﻿//******************************************************************************************************
//  LogSubscriptionFileWriter.cs - Gbtc
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
using System.Diagnostics;
using System.IO;
using System.Threading;
using Gemstone.IO;
using Gemstone.Threading;

// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable

namespace Gemstone.Diagnostics.Utilities;

/// <summary>
/// A log subscription that will write messages to a file
/// </summary>
public sealed class LogSubscriptionFileWriter
    : IDisposable
{
    /// <summary>
    /// When a new file has been completed.
    /// </summary>
    public event Action<string>? NewFileComplete;

    private readonly Lock m_syncRoot;
    private readonly ConcurrentQueue<LogMessage> m_messageQueue;
    private readonly LogSubscriber m_subscriber;
    private readonly int m_maxQueue;
    private string? m_path;
    private VerboseLevel m_verbose;
    private LogFileWriter? m_writer;
    private int m_maxFileCount;
    private readonly Stopwatch m_fileAge = new();
    private readonly ScheduledTask m_flushTask;

    // A number that increments every time the service has restarted. This will help to identify order and when the service last restarted
    private int m_fileSequenceNumber;
    private readonly string m_processName;

    /// <summary>
    /// Creates a LogFileWriter that initially queues message
    /// </summary>
    /// <param name="messageLimit">the number of messages to maintain</param>
    public LogSubscriptionFileWriter(int messageLimit)
    {
        m_processName = Process.GetCurrentProcess().ProcessName;
        m_fileSequenceNumber = 1;
        m_syncRoot = new Lock();
        m_maxQueue = messageLimit;
        m_messageQueue = new ConcurrentQueue<LogMessage>();

        m_flushTask = new ScheduledTask();
        m_flushTask.Running += m_flushTask_Running;

        m_verbose = VerboseLevel.High;
        m_subscriber = Logger.CreateSubscriber(m_verbose);
        m_subscriber.SubscribeToAll(m_verbose);
        m_subscriber.NewLogMessage += SubscriberNewLogMessage;
    }

    private void m_flushTask_Running(object? sender, EventArgs e)
    {
        if (m_path is null)
            return;

        bool hasWrittenLogMessage = false;
        while (m_messageQueue.TryDequeue(out LogMessage? message))
        {
            WriteLogMessage(message);
            hasWrittenLogMessage = true;
        }

        if (hasWrittenLogMessage)
            m_writer?.Flush();
    }

    /// <summary>
    /// Gets or sets verbosity level for this <see cref="LogSubscriptionFileWriter"/>.
    /// </summary>
    public VerboseLevel Verbose
    {
        get
        {
            return m_verbose;
        }
        set
        {
            m_verbose = value;
            m_subscriber.SubscribeToAll(m_verbose);
        }
    }

    /// <summary>
    /// Sets the maximum number of log files before a new one will delete the oldest one.
    /// </summary>
    /// <param name="maxFileCount"></param>
    public void SetLoggingFileCount(int maxFileCount)
    {
        lock (m_syncRoot)
        {
            m_maxFileCount = maxFileCount;
        }
    }

    /// <summary>
    /// Sets the path of the log files.
    /// </summary>
    /// <param name="logDirectory"></param>
    public void SetPath(string logDirectory)
    {
        FilePath.ValidatePathName(logDirectory);

        lock (m_syncRoot)
        {
            m_path = logDirectory;
            m_flushTask.Start(1000);
        }
    }

    /// <summary>
    /// Sets log file writer path and optionally its verbosity level.
    /// </summary>
    public void SetPath(string logDirectory, VerboseLevel level)
    {
        FilePath.ValidatePathName(logDirectory);

        lock (m_syncRoot)
        {
            Verbose = level;
            m_path = logDirectory;
            m_flushTask.Start(1000);
        }
    }

    private void SubscriberNewLogMessage(LogMessage logMessage)
    {
        lock (m_syncRoot)
        {
            if (m_path is null)
            {
                if (m_messageQueue.Count >= m_maxQueue)
                    m_messageQueue.TryDequeue(out _);

                m_messageQueue.Enqueue(logMessage);
            }
            else
            {
                m_messageQueue.Enqueue(logMessage);
                m_flushTask.Start(1000);
            }
        }
    }

    private void WriteLogMessage(LogMessage log)
    {
        //Rollover a file if over 500KB in size, over 50000 records, or older than 12 hours.
        //Ideally, 500KB will always come first.
        if (m_writer is not null && (m_writer.LogSize > 500 * 1024 || m_writer.LogCount >= 50000 || m_fileAge.Elapsed.TotalHours > 12))
        {
            string fileName = m_writer.FileName;
            m_writer.Dispose();
            m_writer = null;
            OnNewFileComplete(fileName);
        }

        if (m_writer is null)
        {
            m_fileAge.Restart();

            try
            {
                if (m_maxFileCount > 0)
                {
                    string[] files = Directory.GetFiles(m_path!, "*.logz");

                    if (files.Length > m_maxFileCount)
                    {
                        Array.Sort(files);

                        for (int x = 0; x < files.Length - m_maxFileCount; x++) 
                            File.Delete(files[x]);
                    }
                }
            }
            catch
            {
                // ignored
            }

            string file = $"{DateTime.UtcNow:yyyyMMdd-HHmmss-ffffff} - {m_processName} - {m_fileSequenceNumber}.logz";
            m_fileSequenceNumber++;

            string fileName = Path.Combine(m_path!, file);
            m_writer = new LogFileWriter(fileName);
        }

        m_writer.Write(log, false);
    }

    /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
    /// <filterpriority>2</filterpriority>
    public void Dispose()
    {
        m_flushTask.Dispose();

        lock (m_syncRoot)
        {
            if (m_writer is null)
                return;

            string fileName = m_writer.FileName;
            m_writer.Dispose();
            m_writer = null;

            OnNewFileComplete(fileName);
        }
    }

    private void OnNewFileComplete(string fileName)
    {
        Action<string>? action = NewFileComplete;

        if (action is null)
            return;

        try
        {
            action(fileName);
        }
        catch
        {
            // ignored
        }
    }
}
