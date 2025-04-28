﻿//******************************************************************************************************
//  LogMessage.cs - Gbtc
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
using System.Reflection;
using System.Text;
using Gemstone.Diagnostics.Internal;
using Gemstone.Diagnostics.Internal.Immutable;
using Gemstone.IO.StreamExtensions;

// ReSharper disable InconsistentNaming

namespace Gemstone.Diagnostics;

/// <summary>
/// An individual log message.
/// </summary>
public sealed class LogMessage
{
    /// <summary>
    /// Contains details about the <see cref="LogEventPublisher"/> that published this <see cref="LogMessage"/>.
    /// </summary>
    public readonly LogEventPublisherDetails EventPublisherDetails;

    /// <summary>
    /// The message stack that existed when the <see cref="LogPublisher"/> was originally constructed.
    /// </summary>
    public readonly LogStackMessages InitialStackMessages;

    /// <summary>
    /// The stack trace that existed when the <see cref="LogPublisher"/> was originally constructed.
    /// </summary>
    public readonly LogStackTrace InitialStackTrace;

    /// <summary>
    /// The message stack that existed when this <see cref="LogMessage"/> was published.
    /// </summary>
    public readonly LogStackMessages CurrentStackMessages;

    /// <summary>
    /// The stack trace that existed when this <see cref="LogMessage"/> was published.
    /// </summary>
    public readonly LogStackTrace CurrentStackTrace;

    /// <summary>
    /// The time that the message was created.
    /// </summary>
    public readonly DateTime UtcTime;

    /// <summary>
    /// The classification of the message
    /// </summary>
    public MessageClass Classification => LogMessageAttributes.Classification;

    /// <summary>
    /// The level associated with the message
    /// </summary>
    public MessageLevel Level => LogMessageAttributes.Level;

    /// <summary>
    /// The flags associated with the message
    /// </summary>
    public MessageFlags Flags => LogMessageAttributes.Flags;

    /// <summary>
    /// The suppression level assigned to this log message
    /// </summary>
    public MessageSuppression MessageSuppression => LogMessageAttributes.MessageSuppression;

    /// <summary>
    /// The <see cref="Type"/> associated with <see cref="LogPublisher"/> that generated the message.
    /// </summary>
    public string TypeName => EventPublisherDetails.TypeData.TypeName;

    /// <summary>
    /// The <see cref="Assembly"/> associated with <see cref="LogPublisher"/> that generated the message.
    /// </summary>
    public string AssemblyName => EventPublisherDetails.TypeData.AssemblyName;

    /// <summary>
    /// All related types such as interfaces/parent classes for the current type.
    /// </summary>
    internal ImmutableList<string> RelatedTypes => EventPublisherDetails.TypeData.RelatedTypes;

    /// <summary>
    /// The event name of this log message.
    /// </summary>
    public string EventName => EventPublisherDetails.EventName;

    internal readonly LogMessageAttributes LogMessageAttributes;

    /// <summary>
    /// A specific message about the event giving more specifics about the actual message. 
    /// Typically, this will be up to 1 line of text. 
    /// Can be String.Empty.
    /// </summary>
    public readonly string Message;

    /// <summary>
    /// A long text field with the details of the message. 
    /// Can be String.Empty.
    /// </summary>
    public readonly string Details;

    /// <summary>
    /// An exception object if one is provided.
    /// Can be null. 
    /// Since the exception is not serialized to the disk, it will be null when loaded.
    /// </summary>
    public readonly Exception? Exception;

    /// <summary>
    /// A string representation of the exception. Can be String.Empty.
    /// If loaded from the disk, since exception objects cannot be serialized, 
    /// the <see cref="Exception"/> will be null and 
    /// this field will have the string representation of <see cref="Exception"/>
    /// </summary>
    public readonly string ExceptionString;

    /// <summary>
    /// The Managed Thread ID of the thread that created this message. This 
    /// is primarily to assist in future log viewing applications
    /// where it is beneficial to track the thread.
    /// </summary>
    public readonly int ManagedThreadID;

    /// <summary>
    /// A sequence number maintained by each thread of the previous 
    /// first chance exception that was thrown. This is used to assist
    /// LogFileViewer associate log messages with properly handled 
    /// first chance exceptions.
    /// </summary>
    public readonly int PreviousFirstChanceExceptionSequenceNumber;

    /// <summary>
    /// Loads a log messages from the supplied stream
    /// </summary>
    /// <param name="stream">the stream to load the log message from.</param>
    /// <param name="saveHelper">A save helper that will condense objects</param>
    internal LogMessage(Stream stream, LogMessageSaveHelper? saveHelper = null)
    {
        saveHelper ??= LogMessageSaveHelper.Create(true);

        byte version = stream.ReadNextByte();
        switch (version)
        {
            case 1:
                EventPublisherDetails = saveHelper.LoadEventPublisherDetails(stream);
                InitialStackMessages = saveHelper.LoadStackMessages(stream);
                InitialStackTrace = saveHelper.LoadStackTrace(stream);
                CurrentStackMessages = saveHelper.LoadStackMessages(stream);
                CurrentStackTrace = saveHelper.LoadStackTrace(stream);
                UtcTime = stream.ReadDateTime();
                LogMessageAttributes = new LogMessageAttributes(stream);
                Message = stream.ReadString();
                Details = stream.ReadString();
                Exception = null;
                ExceptionString = stream.ReadString();
                ManagedThreadID = -1;
                PreviousFirstChanceExceptionSequenceNumber = -1;
                break;
            case 2:
                EventPublisherDetails = saveHelper.LoadEventPublisherDetails(stream);
                InitialStackMessages = saveHelper.LoadStackMessages(stream);
                InitialStackTrace = saveHelper.LoadStackTrace(stream);
                CurrentStackMessages = saveHelper.LoadStackMessages(stream);
                CurrentStackTrace = saveHelper.LoadStackTrace(stream);
                UtcTime = stream.ReadDateTime();
                LogMessageAttributes = new LogMessageAttributes(stream);
                Message = stream.ReadString();
                Details = stream.ReadString();
                Exception = null;
                ExceptionString = stream.ReadString();
                ManagedThreadID = stream.ReadInt32();
                PreviousFirstChanceExceptionSequenceNumber = -1;
                break;
            case 3:
                EventPublisherDetails = saveHelper.LoadEventPublisherDetails(stream);
                InitialStackMessages = saveHelper.LoadStackMessages(stream);
                InitialStackTrace = saveHelper.LoadStackTrace(stream);
                CurrentStackMessages = saveHelper.LoadStackMessages(stream);
                CurrentStackTrace = saveHelper.LoadStackTrace(stream);
                UtcTime = stream.ReadDateTime();
                LogMessageAttributes = new LogMessageAttributes(stream);
                Message = saveHelper.LoadString(stream);
                Details = saveHelper.LoadString(stream);
                Exception = null;
                ExceptionString = saveHelper.LoadString(stream);
                ManagedThreadID = stream.ReadInt32();
                PreviousFirstChanceExceptionSequenceNumber = -1;
                break;
            case 4:
                EventPublisherDetails = saveHelper.LoadEventPublisherDetails(stream);
                InitialStackMessages = saveHelper.LoadStackMessages(stream);
                InitialStackTrace = saveHelper.LoadStackTrace(stream);
                CurrentStackMessages = saveHelper.LoadStackMessages(stream);
                CurrentStackTrace = saveHelper.LoadStackTrace(stream);
                UtcTime = stream.ReadDateTime();
                LogMessageAttributes = new LogMessageAttributes(stream);
                Message = saveHelper.LoadString(stream);
                Details = saveHelper.LoadString(stream);
                Exception = null;
                ExceptionString = saveHelper.LoadString(stream);
                ManagedThreadID = stream.ReadInt32();
                PreviousFirstChanceExceptionSequenceNumber = stream.ReadInt32();
                break;
            default:
                throw new VersionNotFoundException();
        }
    }

    /// <summary>
    /// Creates a log message
    /// </summary>
    /// <exception cref="ArgumentNullException"></exception>
    internal LogMessage(LogEventPublisherDetails? eventPublisherDetails, LogStackMessages? initialStackMessages, LogStackTrace? initialStackTrace, LogStackMessages? currentStackMessages, LogStackTrace? currentStackTrace, LogMessageAttributes flags, string?message, string? details, Exception? exception)
    {
        ExceptionString = exception is null ? string.Empty : exception.ToString();

        EventPublisherDetails = eventPublisherDetails ?? throw new ArgumentNullException(nameof(eventPublisherDetails));
        InitialStackMessages = initialStackMessages ?? LogStackMessages.Empty;
        InitialStackTrace = initialStackTrace ?? LogStackTrace.Empty;
        CurrentStackMessages = currentStackMessages ?? LogStackMessages.Empty;
        CurrentStackTrace = currentStackTrace ?? LogStackTrace.Empty;
        UtcTime = DateTime.UtcNow;
        LogMessageAttributes = flags;
        Message = message ?? string.Empty;
        Details = details ?? string.Empty;
        Exception = exception;
        ManagedThreadID = Environment.CurrentManagedThreadId;
        PreviousFirstChanceExceptionSequenceNumber = Logger.PreviousFirstChanceExceptionSequenceNumber;
    }

    /// <summary>Returns a string that represents the current object.</summary>
    /// <returns>A string that represents the current object.</returns>
    /// <filterpriority>2</filterpriority>
    public override string ToString() => 
        GetMessage();

    /// <summary>
    /// Writes the log data to the stream
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="saveHelper"></param>
    internal void Save(Stream stream, LogMessageSaveHelper? saveHelper = null)
    {
        saveHelper ??= LogMessageSaveHelper.Create(true);

        stream.Write((byte)4);
        saveHelper.SaveEventPublisherDetails(stream, EventPublisherDetails);
        saveHelper.SaveStackMessages(stream, InitialStackMessages);
        saveHelper.SaveStackTrace(stream, InitialStackTrace);
        saveHelper.SaveStackMessages(stream, CurrentStackMessages);
        saveHelper.SaveStackTrace(stream, CurrentStackTrace);
        stream.Write(UtcTime);
        LogMessageAttributes.Save(stream);
        saveHelper.SaveString(stream, Message);
        saveHelper.SaveString(stream, Details);
        saveHelper.SaveString(stream, ExceptionString);
        stream.Write(ManagedThreadID);
        stream.Write(PreviousFirstChanceExceptionSequenceNumber);
    }

    /// <summary>
    /// Gets the details of the message.
    /// </summary>
    /// <returns></returns>
    public string GetMessage()
    {
        StringBuilder sb = new();

        sb.Append("Time: ");
        sb.Append(UtcTime.ToLocalTime());
        sb.Append(" - ");
        sb.Append(Classification.ToString());
        sb.Append(" - ");
        sb.Append(Level.ToString());
        sb.Append(" - ");
        sb.Append(Flags.ToString());
        sb.Append(" - ");
        sb.Append(MessageSuppression.ToString());
        sb.AppendLine();
        sb.Append("Event Name: ");
        sb.AppendLine(EventPublisherDetails.EventName);

        if (Message.Length > 0)
        {
            sb.Append("Message: ");
            sb.AppendLine(Message);
        }

        if (Details.Length > 0)
        {
            sb.Append("Details: ");
            sb.AppendLine(Details);
        }

        if (ExceptionString.Length > 0)
        {
            sb.AppendLine("Exception: ");
            sb.AppendLine(ExceptionString);
        }

        if (EventPublisherDetails.TypeData.TypeName.Length > 0)
        {
            sb.AppendLine("Message Type: " + EventPublisherDetails.TypeData.TypeName);
        }

        if (EventPublisherDetails.TypeData.AssemblyName.Length > 0)
        {
            sb.AppendLine($"Message Assembly: {EventPublisherDetails.TypeData.AssemblyName} ({EventPublisherDetails.TypeData.AssemblyVersion}) ");
        }

        sb.AppendLine($"Managed Thread Id: {ManagedThreadID}");

        if (!ReferenceEquals(InitialStackMessages, LogStackMessages.Empty))
        {
            sb.AppendLine();
            sb.AppendLine("Initial Stack Messages: ");
            sb.AppendLine(InitialStackMessages.ToString());
        }

        if (!ReferenceEquals(InitialStackTrace, LogStackTrace.Empty))
        {
            sb.AppendLine();
            sb.AppendLine("Initial Stack Trace: ");
            sb.AppendLine(InitialStackTrace.ToString());
        }

        if (!ReferenceEquals(CurrentStackMessages, LogStackMessages.Empty))
        {
            sb.AppendLine();
            sb.AppendLine("Current Stack Messages: ");
            sb.AppendLine(CurrentStackMessages.ToString());
        }

        if (!ReferenceEquals(CurrentStackTrace, LogStackTrace.Empty))
        {
            sb.AppendLine();
            sb.AppendLine("Current Stack Trace: ");
            sb.AppendLine(CurrentStackTrace.ToString());
        }

        return sb.ToString();
    }
}
