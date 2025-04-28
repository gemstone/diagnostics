﻿//******************************************************************************************************
//  LoggerInternal.cs - Gbtc
//
//  Copyright © 2016, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the Eclipse Public License -v 1.0 (the "License"); you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://www.opensource.org/licenses/eclipse-1.0.php
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  10/24/2016 - Steven E. Chisholm
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Gemstone.Collections.CollectionExtensions;
using Gemstone.Threading;

namespace Gemstone.Diagnostics.Internal;

/// <summary>
/// The fundamental functionality of <see cref="Logger"/>.
/// </summary>
internal class LoggerInternal
    : IDisposable
{
    private bool m_disposing;
    private readonly Lock m_syncRoot;
    private readonly Dictionary<Type, LogPublisherInternal> m_typeIndexCache;
    private readonly List<LogPublisherInternal> m_allPublishers;
    private readonly List<NullableWeakReference> m_subscribers;
    private readonly ConcurrentQueue<Tuple<LogMessage, LogPublisherInternal>> m_messages;
    private readonly ScheduledTask m_routingTask;
    private readonly ScheduledTask m_calculateRoutingTable;
    public bool RoutingTablesValid { get; private set; }

    /// <summary>
    /// Creates a <see cref="LoggerInternal"/>.
    /// </summary>
    public LoggerInternal(out LoggerInternal loggerClass)
    {
        //Needed to set the member variable of Logger. 
        //This is because ScheduleTask will call Logger before Logger's static constructor is completed.
        loggerClass = this;

        m_syncRoot = new Lock();
        m_typeIndexCache = new Dictionary<Type, LogPublisherInternal>();
        m_allPublishers = [];
        m_subscribers = [];
        m_messages = new ConcurrentQueue<Tuple<LogMessage, LogPublisherInternal>>();

        // Since ScheduledTask calls ShutdownHandler, which calls Logger. This initialization method cannot occur
        // until after the Logger static class has finished initializing.
        m_calculateRoutingTable = new ScheduledTask();
        m_calculateRoutingTable.Running += CalculateRoutingTable;
        m_calculateRoutingTable.IgnoreShutdownEvent();
        m_routingTask = new ScheduledTask(ThreadingMode.DedicatedForeground);
        m_routingTask.Running += RoutingTask;
        m_routingTask.IgnoreShutdownEvent();
    }

    /// <summary>
    /// Recalculates the entire routing table on a separate thread.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void CalculateRoutingTable(object? sender, EventArgs e)
    {
        lock (m_syncRoot)
        {
            //Some other thread won on the race condition.
            if (RoutingTablesValid)
                return;

            List<LogSubscriberInternal> subscribers = new(m_subscribers.Count);
            
            m_subscribers.RemoveWhere(x =>
            {
                if (x.Target is not LogSubscriberInternal subscriber)
                    return true;
                
                subscribers.Add(subscriber);
                return false;
            });

            foreach (LogPublisherInternal pub in m_allPublishers)
                CalculateRoutingTableForPublisherSync(subscribers, pub);
            
            RoutingTablesValid = true;
        }
    }

    /// <summary>
    /// This method should be called with a lock on m_syncRoot
    /// </summary>
    private void CalculateRoutingTableForPublisherSync(List<LogSubscriberInternal> subscribers, LogPublisherInternal publisher)
    {
        MessageAttributeFilterCollection filterCollection = new();

        foreach (LogSubscriberInternal sub in subscribers) 
            filterCollection.Add(sub.GetSubscription(publisher), sub);

        publisher.SubscriptionFilterCollection = filterCollection;
    }

    private void RoutingTask(object? sender, EventArgs e)
    {
        lock (m_syncRoot)
        {
            if (!RoutingTablesValid) 
                CalculateRoutingTable(null, null!);

            while (m_messages.TryDequeue(out Tuple<LogMessage, LogPublisherInternal>? messageTuple))
            {
                LogPublisherInternal publisher = messageTuple.Item2;
                LogMessage message = messageTuple.Item1;

                foreach (Tuple<MessageAttributeFilter, NullableWeakReference> route in publisher.SubscriptionFilterCollection.Routes)
                {
                    MessageAttributeFilter filter = route.Item1;

                    if (route.Item2.Target is not LogSubscriberInternal subscriber)
                        RecalculateRoutingTable();
                    else if (filter.IsSubscribedTo(message.LogMessageAttributes))
                        subscriber.RaiseLogMessages(message);
                }
            }
        }
    }

    /// <summary>
    /// Creates a <see cref="LogSubscriber"/> that can subscribe to log messages.
    /// </summary>
    /// <returns></returns>
    public LogSubscriberInternal CreateSubscriber()
    {
        lock (m_syncRoot)
        {
            if (m_disposing)
                return LogSubscriberInternal.DisposedSubscriber;

            LogSubscriberInternal s = new(RecalculateRoutingTable);
            m_subscribers.Add(s.Reference);

            return s;
        }
    }

    /// <summary>
    /// Invalidates the current routing table.
    /// </summary>
    private void RecalculateRoutingTable()
    {
        RoutingTablesValid = false;

        //Wait some time before recalculating the routing tables. This will be done automatically 
        //if a message is routed before the table is recalculated.
        m_calculateRoutingTable.Start(10);
    }

    /// <summary>
    /// Handles the routing of messages through the logging system.
    /// </summary>
    /// <param name="message">the message to route</param>
    /// <param name="publisher">the publisher that is originating this message.</param>
    public void OnNewMessage(LogMessage message, LogPublisherInternal publisher)
    {
        m_messages.Enqueue(Tuple.Create(message, publisher));
        m_routingTask.Start(50); //Allow a 50ms delay for multiple logs to queue if in a burst period.
    }

    /// <summary>
    /// Creates a type topic on a specified type.
    /// </summary>
    /// <param name="type">the type to create the topic from</param>
    /// <returns></returns>
    public LogPublisherInternal CreateType(Type type)
    {
        if (type is null)
            throw new ArgumentNullException(nameof(type));

        LogPublisherInternal? publisher;

        lock (m_syncRoot)
        {
            if (m_typeIndexCache.TryGetValue(type, out publisher))
                return publisher;

            publisher = new LogPublisherInternal(this, type);
            m_typeIndexCache.Add(type, publisher);

            if (m_disposing)
                return publisher;
            
            m_allPublishers.Add(publisher);
            List<LogSubscriberInternal> lst = [];
            
            foreach (NullableWeakReference logSubscriberInternal in m_subscribers)
            {
                if (logSubscriberInternal.Target is LogSubscriberInternal target) 
                    lst.Add(target);
            }
            
            CalculateRoutingTableForPublisherSync(lst, publisher);
        }
        
        return publisher;
    }

    /// <summary>
    /// Gracefully terminate all message routing. Function blocks until all termination is successful.
    /// </summary>
    public void Dispose()
    {
        if (m_disposing)
            return;

        lock (m_syncRoot)
        {
            //Ensure that setting disposing is in a synchronized context.
            m_disposing = true;
        }

        //These scheduled tasks block when dispose is called. Therefore do not put these in a lock on the base class.
        m_calculateRoutingTable.Dispose();
        m_routingTask.Dispose();

        lock (m_syncRoot)
        {
            m_allPublishers.Clear();
            
            foreach (NullableWeakReference sub in m_subscribers) 
                (sub.Target as LogSubscriberInternal)?.Dispose();
            
            m_subscribers.Clear();
        }
    }
}
