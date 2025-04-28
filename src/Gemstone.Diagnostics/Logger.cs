//******************************************************************************************************
//  Logger.cs - Gbtc
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
// ReSharper disable InconsistentNaming
// ReSharper disable MemberHidesStaticFromOuterClass

using System;
using System.Collections.Generic;
using System.Threading;
using Gemstone.Configuration;
using Gemstone.Diagnostics.Internal;
using Gemstone.Diagnostics.Utilities;
using Gemstone.Threading;
using Microsoft.CSharp.RuntimeBinder;

namespace Gemstone.Diagnostics;

/// <summary>
/// Manages the collection and reporting of logging information in a system.
/// </summary>
public static class Logger
{
    private static class ThreadLocalThreadStack
    {
        [ThreadStatic]
        private static ThreadStack? s_localValue;

        /// <summary>
        /// Gets the <see cref="ThreadStack"/> item for the current thread.
        /// Note: No exchange compare is needed since <see cref="s_localValue"/>
        /// is local only to the current thread.
        /// </summary>
        public static ThreadStack Value => s_localValue ??= new ThreadStack();
    }

    /// <summary>
    /// This information is maintained in a ThreadLocal variable and is about 
    /// messages and log suppression applied at higher levels of the calling stack.
    /// </summary>
    private class ThreadStack
    {
        public int PreviousFirstChanceExceptionSequenceNumber;
        private readonly List<LogStackMessages> m_threadStackDetails = [];
        private LogStackMessages? m_stackMessageCache;

        public LogStackMessages GetStackMessages()
        {
            if (m_stackMessageCache is null)
            {
                m_stackMessageCache = m_threadStackDetails.Count == 0 ? 
                    LogStackMessages.Empty : 
                    new LogStackMessages(m_threadStackDetails);
            }

            return m_stackMessageCache;
        }

        public StackDisposal AppendStackMessages(LogStackMessages messages)
        {
            m_stackMessageCache = null;
            m_threadStackDetails.Add(messages);

            int depth = m_threadStackDetails.Count;
            
            if (depth >= s_stackDisposalStackMessages!.Length) 
                GrowStackDisposal(depth + 1);
            
            return s_stackDisposalStackMessages[depth];
        }

        public void RemoveStackMessage(int depth)
        {
            while (m_threadStackDetails.Count >= depth) 
                m_threadStackDetails.RemoveAt(m_threadStackDetails.Count - 1);
            m_stackMessageCache = null;
        }
    }

    private static readonly LoggerInternal s_logger;

    /// <summary>
    /// The default console based log subscriber.
    /// </summary>
    public static readonly LogSubscriptionConsole Console;

    /// <summary>
    /// The default file based log writer.
    /// </summary>
    public static readonly LogSubscriptionFileWriter FileWriter;

    private static readonly LogPublisher Log;
    private static readonly LogEventPublisher EventFirstChanceException = default!;
    private static readonly LogEventPublisher EventAppDomainException;
    private static readonly LogEventPublisher EventSwallowedException;
    private static StackDisposal[]? s_stackDisposalStackMessages;
    private static readonly Lock SyncRoot = LogSuppression.SyncRoot;

    static Logger()
    {
        //Initializes the empty object of StackTraceDetails
        LogStackTrace.Initialize();
        LogStackMessages.Initialize();
        GrowStackDisposal(1);

        s_logger = new LoggerInternal(out s_logger);
        Console = new LogSubscriptionConsole();
        FileWriter = new LogSubscriptionFileWriter(1000);

        dynamic loggingSettings = Settings.Instance[DiagnosticsLogger.DefaultSettingsCategory];

        bool logFirstChanceExceptions = loggingSettings["LogFirstChanceExceptions", true, "Defines flag that determines if first chance exceptions are logged."];

        if (logFirstChanceExceptions)
            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        Log = CreatePublisher(typeof(Logger), MessageClass.Component);
        Log.InitialStackTrace = LogStackTrace.Empty;

        if (logFirstChanceExceptions)
        {
            int firstChanceExceptionRate = loggingSettings["FirstChanceExceptionRate", DiagnosticsLogger.DefaultRateLimit, "Defines the maximum expected rate at which first change exception messages are logged for rate limiting."];
            int firstChanceExceptionBurstLimit = loggingSettings["FirstChanceExceptionBurstLimit", DiagnosticsLogger.DefaultBurstLimit, "Defines the maximum number of first chance exception messages that can be logged in a burst."];
            
            EventFirstChanceException = Log.RegisterEvent(MessageLevel.Info, MessageFlags.None, "First Chance App Domain Exception", 30, MessageRate.PerSecond(firstChanceExceptionRate), firstChanceExceptionBurstLimit);
        }

        EventAppDomainException = Log.RegisterEvent(MessageLevel.Critical, MessageFlags.SystemHealth, "Unhandled App Domain Exception");

        int swallowedExceptionRate = loggingSettings["SwallowedExceptionRate", DiagnosticsLogger.DefaultRateLimit, "Defines the maximum expected rate at which swallowed exception messages are logged for rate limiting."];
        int swallowedExceptionBurstLimit = loggingSettings["SwallowedExceptionBurstLimit", DiagnosticsLogger.DefaultBurstLimit, "Defines the maximum number of swallowed exception messages that can be logged in a burst."];
        
        EventSwallowedException = Log.RegisterEvent(MessageLevel.Debug, MessageFlags.None, "Exception was Swallowed", 30, MessageRate.PerSecond(swallowedExceptionRate), swallowedExceptionBurstLimit);

        ShutdownHandler.Initialize();
    }

    /// <summary>
    /// Ensures that the logger has been initialized. 
    /// </summary>
    internal static void Initialize()
    {
        //Handled in the static constructor.
    }

    /// <summary>
    /// Ensures that the logger is properly shutdown.
    /// This is called from ShutdownHandler.
    /// </summary>
    internal static void Shutdown()
    {
        try
        {
            Log.Publish(MessageLevel.Critical, MessageFlags.SystemHealth, "Logger is shutting down.");
            s_logger.Dispose();
            Console.Verbose = VerboseLevel.None;
            FileWriter.Dispose();
            //Cannot raise log messages here since the logger is now shutdown.
        }
        catch
        {
            // ignored
        }
    }

    /// <summary>
    /// Gets the sequence number of the most recent First Chance Exception log 
    /// </summary>
    internal static int PreviousFirstChanceExceptionSequenceNumber => ThreadLocalThreadStack.Value.PreviousFirstChanceExceptionSequenceNumber;

    /// <summary>
    /// Gets if Log Messages should be suppressed.
    /// </summary>
    public static bool ShouldSuppressLogMessages => LogSuppression.ShouldSuppressLogMessages;

    /// <summary>
    /// Gets if First Chance Exception Log Messages should be suppressed.
    /// </summary>
    public static bool ShouldSuppressFirstChanceLogMessages => LogSuppression.ShouldSuppressFirstChanceLogMessages;

    private static void CurrentDomain_FirstChanceException(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
    {
        if ((Thread.CurrentThread.ThreadState & (ThreadState.AbortRequested | ThreadState.Aborted)) != 0)
            return;

        if (ShouldSuppressFirstChanceLogMessages)
            return;
        
        // Do not log RuntimeBinderExceptions - these are normal exceptions on dynamic objects when properties are not found.
        if (e.Exception is RuntimeBinderException)
            return;

        using (SuppressFirstChanceExceptionLogMessages())
        {
            try
            {
                EventFirstChanceException.Publish(null, null, e.Exception);
            }
            catch (Exception)
            {
                //swallow any exceptions.
            }
            ThreadLocalThreadStack.Value.PreviousFirstChanceExceptionSequenceNumber++;
        }
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if ((Thread.CurrentThread.ThreadState & (ThreadState.AbortRequested | ThreadState.Aborted)) != 0)
            return;

        using (SuppressFirstChanceExceptionLogMessages())
        {
            try
            {
                EventAppDomainException.Publish(null, null, e.ExceptionObject as Exception);
            }
            catch (Exception)
            {
                //swallow any exceptions.
            }
        }
    }

    /// <summary>
    /// Looks up the type of the log source
    /// </summary>
    /// <param name="type">the type</param>
    /// <param name="classification">the classification of the type of messages that this publisher will raise.</param>
    /// <returns></returns>
    public static LogPublisher CreatePublisher(Type type, MessageClass classification)
    {
        return new LogPublisher(s_logger, s_logger.CreateType(type), classification);
    }

    /// <summary>
    /// Creates a <see cref="LogSubscriber"/>
    /// </summary>
    /// <returns></returns>
    public static LogSubscriber CreateSubscriber(VerboseLevel level = VerboseLevel.None)
    {
        LogSubscriber subscriber = new(s_logger.CreateSubscriber());
        subscriber.SubscribeToAll(level);
        return subscriber;
    }

    /// <summary>
    /// Logs that a first chance exception was intentionally not handled for the provided reason.
    /// In the LogFileViewer it will filter messages differently if it was indicated that they were swallowed.
    /// </summary>
    /// <param name="ex">the exception that was swallowed</param>
    /// <param name="message">message to include, such as a reason why it was swallowed.</param>
    /// <param name="details">additional details.</param>
    /// <param name="additionalFlags">additional flags that can be set with this swallowed exception.</param>
    public static void SwallowException(Exception ex, string? message = null, string? details = null, MessageFlags additionalFlags = MessageFlags.None)
    {
        EventSwallowedException.Publish(additionalFlags, message, details, ex);
        //Increment this value to ensure that nothing else matches with the exception since it has been removed.
        ThreadLocalThreadStack.Value.PreviousFirstChanceExceptionSequenceNumber++;
    }

    /// <summary>
    /// Searches the current stack frame for all related messages that will be published with this message.
    /// </summary>
    /// <returns></returns>
    public static LogStackMessages GetStackMessages()
    {
        return ThreadLocalThreadStack.Value.GetStackMessages();
    }

    /// <summary>
    /// Temporarily appends data to the thread's stack so the data can be propagated to any messages generated on this thread.
    /// Be sure to call Dispose on the returned object to remove this from the stack.
    /// </summary>
    /// <param name="messages"></param>
    /// <returns></returns>
    public static IDisposable AppendStackMessages(LogStackMessages messages)
    {
        return ThreadLocalThreadStack.Value.AppendStackMessages(messages);
    }

    /// <summary>
    /// Sets a flag that will prevent log messages from being raised on this thread.
    /// Remember to dispose of the callback to remove this suppression.
    /// </summary>
    /// <returns></returns>
    public static IDisposable SuppressLogMessages()
    {
        return LogSuppression.SuppressLogMessages();
    }

    /// <summary>
    /// Sets a flag that will prevent First Chance Exception log messages from being raised on this thread.
    /// Remember to dispose of the callback to remove this suppression.
    /// </summary>
    /// <returns></returns>
    public static IDisposable SuppressFirstChanceExceptionLogMessages()
    {
        return LogSuppression.SuppressFirstChanceExceptionLogMessages();
    }

    /// <summary>
    /// Sets a flag that will allow log messages to be raised again.
    /// Remember to dispose of the callback to remove this override.
    /// </summary>
    /// <returns></returns>
    public static IDisposable OverrideSuppressLogMessages()
    {
        return LogSuppression.OverrideSuppressLogMessages();
    }

    /// <summary>
    /// Temporarily appends data to the thread's stack so the data can be propagated to any messages generated on this thread.
    /// Be sure to call Dispose on the returned object to remove this from the stack.
    /// </summary>
    /// <returns></returns>
    public static IDisposable AppendStackMessages(string key, string value)
    {
        return AppendStackMessages(new LogStackMessages(key, value));
    }

    private static void GrowStackDisposal(int desiredSize)
    {
        //Since these depths are relatively small, growing them both together has minor consequence.
        lock (SyncRoot)
        {
            while (s_stackDisposalStackMessages is null || s_stackDisposalStackMessages.Length < desiredSize)
            {
                //Note: both are grown together and completely reinitialized to improve 
                //      locality of reference.
                int lastSize = s_stackDisposalStackMessages?.Length ?? 2;
                StackDisposal[] stackMessages = new StackDisposal[lastSize * 2];
                
                for (int x = 0; x < stackMessages.Length; x++) 
                    stackMessages[x] = new StackDisposal(x, DisposeStackMessage);

                s_stackDisposalStackMessages = stackMessages;
            }
        }
    }

    private static void DisposeStackMessage(int depth)
    {
        ThreadLocalThreadStack.Value.RemoveStackMessage(depth);
    }
}
