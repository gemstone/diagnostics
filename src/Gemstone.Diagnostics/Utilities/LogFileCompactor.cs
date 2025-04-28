//******************************************************************************************************
//  LogFileCompactor.cs - Gbtc
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
using System.Collections.Generic;
using System.IO;
using Gemstone.Diagnostics.Internal.Ionic.Zlib;
using Gemstone.IO.StreamExtensions;

// ReSharper disable MemberCanBePrivate.Local

namespace Gemstone.Diagnostics.Utilities;

/// <summary>
/// A method to read all of the logs in a single file.
/// </summary>
public static class LogFileCompactor
{
    /// <summary>
    /// Reads all log messages from the supplied file.
    /// </summary>
    public static void Compact(ICollection<string> inputFileNames, string outputFileName)
    {
        LogCollection logs = new();

        foreach (string file in inputFileNames)
        {
            using FileStream stream = new(file, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (stream.ReadBoolean())
            {
                //Wrong File Version
                break;
            }
            if (stream.ReadNextByte() != 1)
            {
                //Wrong File Version
                break;
            }

            using var zipStream = new DeflateStream(stream, CompressionMode.Decompress, true);
            using BufferedStream bs = new(zipStream, 8192);

            while (bs.ReadBoolean())
            {
                LogMessage message = new(bs);
                logs.Pass1(message);
            }
        }

        logs.Compact();

        foreach (string file in inputFileNames)
        {
            using FileStream stream = new(file, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (stream.ReadBoolean())
            {
                //Wrong File Version
                break;
            }
            if (stream.ReadNextByte() != 1)
            {
                //Wrong File Version
                break;
            }

            using var zipStream = new DeflateStream(stream, CompressionMode.Decompress, true);
            using BufferedStream bs = new(zipStream, 8192);

            while (bs.ReadBoolean())
            {
                LogMessage message = new(bs);
                logs.Pass2(message);
            }
        }

        logs.Save(outputFileName);
    }

    private class LogCollection
    {
        private readonly Dictionary<LogEventPublisherDetails, MessageByOwner> m_owners;

        public LogCollection()
        {
            m_owners = new Dictionary<LogEventPublisherDetails, MessageByOwner>();
        }

        public void Pass1(LogMessage message)
        {
            if (!m_owners.TryGetValue(message.EventPublisherDetails, out MessageByOwner? m))
            {
                m = new MessageByOwner(message.EventPublisherDetails);
                m_owners.Add(message.EventPublisherDetails, m);
            }

            m.Pass1(message);
        }

        public void Compact()
        {
        }

        public void Pass2(LogMessage message)
        {
            m_owners[message.EventPublisherDetails].Pass2(message);
        }

        public void Save(string outputFileName)
        {
            using LogFileWriter fileWriter = new(outputFileName);

            foreach (MessageByOwner owner in m_owners.Values) 
                owner.Save(fileWriter);
        }
    }

    private class MessageByOwner
    {
        private int m_index;
        public readonly List<MessageData> AllMessages;
        public LogEventPublisherDetails Owner;
        public readonly int KeepWeight = 0;

        public MessageByOwner(LogEventPublisherDetails owner)
        {
            m_index = 0;
            Owner = owner;
            AllMessages = [];
        }

        public void Pass1(LogMessage message)
        {
            AllMessages.Add(new MessageData(message));
        }

        public void Pass2(LogMessage message)
        {
            AllMessages[m_index].Assign(message, KeepWeight);
            m_index++;
        }


        public void Save(LogFileWriter fileWriter)
        {
            foreach (MessageData message in AllMessages)
            {
                if (message.Message is not null) 
                    fileWriter.Write(message.Message);
            }
        }
    }

    private class MessageData
    {
        public readonly MessageClass Classification;
        public readonly MessageLevel Level;
        public readonly int KeepWeighting;
        public readonly DateTime UtcDateTime;
        public LogMessage? Message;

        public MessageData(LogMessage message)
        {
            Classification = message.Classification;
            Level = message.Level;
            KeepWeighting = 0;
            UtcDateTime = message.UtcTime;
            Message = null;
        }

        public void Assign(LogMessage message, int keepWeight)
        {
            if (Classification != message.Classification || Level != message.Level || UtcDateTime != message.UtcTime)
                throw new Exception("Parsing Exception");

            if (KeepWeighting >= keepWeight) 
                Message = message;
        }
    }
}
