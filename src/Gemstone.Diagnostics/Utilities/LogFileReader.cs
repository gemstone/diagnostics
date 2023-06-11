//******************************************************************************************************
//  LogFileReader.cs - Gbtc
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

using System.Collections.Generic;
using System.Data;
using System.IO;
using Gemstone.Diagnostics.Internal;
using Gemstone.Diagnostics.Internal.Ionic.Zlib;
using Gemstone.IO;
using Gemstone.IO.StreamExtensions;

namespace Gemstone.Diagnostics.Utilities;

/// <summary>
/// A method to read all of the logs in a single file.
/// </summary>
public static class LogFileReader
{
    /// <summary>
    /// Reads all log messages from the supplied file.
    /// </summary>
    public static List<LogMessage> Read(string logFileName)
    {
        List<LogMessage> lst = new();
        FilePath.ValidatePathName(logFileName);
        using FileStream stream = new(logFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        try
        {
            int version = stream.ReadInt32();
            
            LogMessageSaveHelper helper = version switch
            {
                282497 => LogMessageSaveHelper.Create(), // VersionNumber: Compressed With LogSaveHelper
                _ => throw new VersionNotFoundException()
            };

            using var zipStream = new DeflateStream(stream, CompressionMode.Decompress, true);
            using BufferedStream bs = new(zipStream);

            while (bs.ReadBoolean())
            {
                LogMessage message = new(bs, helper);
                lst.Add(message);
            }

            bs.Dispose();
        }
        catch (EndOfStreamException)
        {
            // This is ok.
        }
        catch (ZlibException)
        {
            // This is ok.
        }

        return lst;
    }
}
