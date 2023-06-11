﻿//******************************************************************************************************
//  LogFileWriter.cs - Gbtc
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
using System.IO;
using Gemstone.Diagnostics.Internal;
using Gemstone.Diagnostics.Internal.Ionic.Zlib;
using Gemstone.IO;
using Gemstone.IO.StreamExtensions;

namespace Gemstone.Diagnostics.Utilities;

/// <summary>
/// A log subscriber that will log messages to a file.
/// </summary>
public sealed class LogFileWriter
    : IDisposable
{
    private readonly MemoryStream m_tmpStream;
    private readonly object m_syncRoot;
    /// <summary>
    /// The file name
    /// </summary>
    public readonly string FileName;

    private FileStream? m_stream;
    private readonly DeflateStream m_zipStream;
    private readonly LogMessageSaveHelper m_saveHelper;
    private readonly byte[] m_tmpBuffer;

    /// <summary>
    /// Creates a LogFileWriter that initially queues message
    /// </summary>
    public LogFileWriter(string logFileName)
    {
        const CompressionLevel Level = CompressionLevel.Level1;

        FileName = logFileName;
        m_saveHelper = LogMessageSaveHelper.Create();

        FilePath.ValidatePathName(logFileName);
        m_stream = new FileStream(logFileName, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
        m_stream.Write(282497); //VersionNumber: Compressed With LogSaveHelper

        m_zipStream = new DeflateStream(m_stream, CompressionMode.Compress, Level, true)
        {
            FlushMode = FlushType.Sync
        };

        m_tmpBuffer = new byte[40960];
        m_tmpStream = new MemoryStream();
        LogCount = 0;
        m_syncRoot = new object();
    }


    /// <summary>
    /// The number of logs that have been written to this file.
    /// </summary>
    public int LogCount { get; private set; }

    /// <summary>
    /// The current size of the log file.
    /// </summary>
    public long LogSize => m_stream?.Length ?? 0;

    /// <summary>
    /// Writes the specified log to the file
    /// </summary>
    /// <param name="log"></param>
    /// <param name="autoFlush"></param>
    public void Write(LogMessage log, bool autoFlush = true)
    {
        m_tmpStream.Position = 0;
        m_tmpStream.Write(true);
        
        log.Save(m_tmpStream, m_saveHelper);
        long length = m_tmpStream.Position;
        
        m_tmpStream.Position = 0;
        m_tmpStream.CopyTo(m_zipStream, length, m_tmpBuffer);

        if (log.Level >= MessageLevel.Info && autoFlush)
            m_zipStream.Flush();

        LogCount++;
    }

    /// <summary>
    /// Flushes the stream to the disk.
    /// </summary>
    public void Flush() => 
        m_zipStream.Flush();

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    /// <filterpriority>2</filterpriority>
    public void Dispose()
    {
        lock (m_syncRoot)
        {
            if (m_stream is null)
                return;

            try
            {
                m_zipStream.Write(false);
                m_zipStream.Flush();
                m_zipStream.Dispose();
                m_stream.Write(false);
                m_stream.Dispose();
                m_tmpStream.Dispose();
            }
            catch
            {
                // ignored
            }

            m_stream = null;
            LogCount = 0;
        }
    }
}
