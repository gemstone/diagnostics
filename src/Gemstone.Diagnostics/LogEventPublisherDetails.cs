﻿//******************************************************************************************************
//  LogEventPublisherDetails.cs - Gbtc
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
using System.Data;
using System.IO;
using System.Text;
using Gemstone.Diagnostics.Internal;
using Gemstone.IO.StreamExtensions;

namespace Gemstone.Diagnostics;

/// <summary>
/// Information about the <see cref="LogEventPublisher"/> that published this message.
/// </summary>
/// <remarks>
/// Since it is likely that a log file will have this data repeated a bunch, this class allows
/// de-duplication of this data so it takes up less memory to store.
/// </remarks>
public sealed class LogEventPublisherDetails
    : IEquatable<LogEventPublisherDetails>
{
    /// <summary>
    /// The <see cref="PublisherTypeDefinition"/> associated with <see cref="LogPublisher"/> that generated the message.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
    public readonly PublisherTypeDefinition TypeData;

    /// <summary>
    /// The event name of this log message.
    /// </summary>
    public readonly string EventName;

    /// <summary>
    /// A hashCode code of this owner.
    /// </summary>
    private readonly int m_hashCode;

    /// <summary>
    /// Loads a log messages from the supplied stream
    /// </summary>
    /// <param name="stream">the stream to load the log message from.</param>
    /// <param name="helper">the helper to assist in loading/saving</param>
    internal LogEventPublisherDetails(Stream stream, LogMessageSaveHelper helper)
    {
        byte version = stream.ReadNextByte();

        switch (version)
        {
            case 1:
                string typeName = stream.ReadString();
                string assemblyName = stream.ReadString();
                TypeData = new PublisherTypeDefinition(typeName, assemblyName);
                EventName = stream.ReadString();
                break;
            case 2:
                EventName = stream.ReadString();
                TypeData = helper.LoadPublisherTypeDefinition(stream);
                break;
            default:
                throw new VersionNotFoundException();
        }

        m_hashCode = ComputeHashCode();
    }

    /// <summary>
    /// Represents a single owner of a log message.
    /// </summary>
    public LogEventPublisherDetails(PublisherTypeDefinition typeData, string? eventName)
    {
        eventName ??= string.Empty;
        TypeData = typeData ?? throw new ArgumentNullException(nameof(typeData));
        EventName = eventName;
        m_hashCode = ComputeHashCode();
    }

    private int ComputeHashCode()
    {
        return TypeData.GetHashCode() ^ EventName.GetHashCode();
    }

    /// <summary>
    /// Writes the log data to the stream
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="helper">the helper to assist in loading/saving</param>
    internal void Save(Stream stream, LogMessageSaveHelper helper)
    {
        stream.Write((byte)2);
        stream.Write(EventName);
        helper.SavePublisherTypeDefinition(stream, TypeData);
    }

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>
    /// A string that represents the current object.
    /// </returns>
    /// <filterpriority>2</filterpriority>
    public override string ToString()
    {
        StringBuilder sb = new();

        if (TypeData.TypeName.Length > 0)
            sb.AppendLine("Message Type: " + TypeData.TypeName);
        
        if (TypeData.AssemblyName.Length > 0) 
            sb.AppendLine("Message Assembly: " + TypeData.AssemblyName);
        
        if (EventName.Length > 0) 
            sb.AppendLine("Event: " + EventName);
        
        sb.Length -= Environment.NewLine.Length;
        
        return sb.ToString();
    }

    /// <summary>
    /// Serves as a hash function for a particular type. 
    /// </summary>
    /// <returns>
    /// A hash code for the current <see cref="T:System.Object"/>.
    /// </returns>
    /// <filterpriority>2</filterpriority>
    public override int GetHashCode() => 
        m_hashCode;

    /// <summary>
    /// Determines whether the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>.
    /// </summary>
    /// <returns>
    /// true if the specified object  is equal to the current object; otherwise, false.
    /// </returns>
    /// <param name="obj">The object to compare with the current object. </param><filterpriority>2</filterpriority>
    public override bool Equals(object? obj) => 
        Equals(obj as LogEventPublisherDetails);

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <returns>
    /// true if the current object is equal to the <paramref name="obj"/> parameter; otherwise, false.
    /// </returns>
    /// <param name="obj">An object to compare with this object.</param>
    public bool Equals(LogEventPublisherDetails? obj)
    {
        if (ReferenceEquals(obj, null))
            return false;

        if (ReferenceEquals(this, obj))
            return true;

        return m_hashCode == obj.m_hashCode &&
               EventName == obj.EventName &&
               TypeData.Equals(obj.TypeData);
    }
}
