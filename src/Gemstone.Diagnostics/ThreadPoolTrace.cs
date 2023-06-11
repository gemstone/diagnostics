//******************************************************************************************************
//  ThreadPoolTrace.cs - Gbtc
//
//  Copyright © 2016, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://www.opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  11/19/2016 - Steven E. Chisholm
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Collections;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Timers;
using Gemstone.Reflection.MethodBaseExtensions;
using Timer = System.Timers.Timer;

// ReSharper disable InconsistentNaming
// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable

namespace Gemstone.Diagnostics;

/// <summary>
/// Executes a trace on the <see cref="ThreadPool"/> for all callbacks currently queued.
/// This will not include work items that have a time delayed callback.
/// </summary>
/// <remarks>
/// This class heavily relies on reflection to get the ThreadPool queue.
/// Therefore it is very unlikely to work in MONO and can break 
/// if Microsoft changes any of the member names or how the ThreadPool 
/// works.
/// 
/// In this case <see cref="WorksInThisRuntime"/> will be set to false
/// and <see cref="GetTrace"/> will return "Not Supported"
/// 
/// </remarks>
public static class ThreadPoolTrace
{
    private static readonly LogPublisher Log = Logger.CreatePublisher(typeof(ThreadPoolTrace), MessageClass.Component);
    /// <summary>
    /// Indicates that this trace works in the runtime version of .NET.
    /// </summary>
    public static bool WorksInThisRuntime { get; private set; }

    private static readonly Type? s_threadPoolType;
    private static readonly MethodInfo? s_threadPoolGetQueuedWorkItemsMethod;

    private static readonly Type? s_queueWorkerCallbackType;
    private static readonly FieldInfo? s_callbackField;
    private static readonly FieldInfo? s_stateField;

    private static readonly Type? s_timerQueueTimerType;
    private static readonly FieldInfo? s_timerQueueTimerTimerCallbackField;

    private static readonly Type? s_timerQueueType;
    private static readonly MethodInfo? s_timerQueueFireQueuedTimerCompletionMethod;

    private static readonly Type? s_timerType;
    private static readonly FieldInfo? s_timerOnIntervalElapsedField;

    static ThreadPoolTrace()
    {
        WorksInThisRuntime = false;

        try
        {
            s_threadPoolType = typeof(ThreadPool);
            s_threadPoolGetQueuedWorkItemsMethod = s_threadPoolType.GetMethod("GetQueuedWorkItems", BindingFlags.Static | BindingFlags.NonPublic);

            s_queueWorkerCallbackType = Type.GetType("System.Threading.QueueUserWorkItemCallback");
            s_callbackField = s_queueWorkerCallbackType?.GetField("callback", BindingFlags.NonPublic | BindingFlags.Instance);
            s_stateField = s_queueWorkerCallbackType?.GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
            s_timerQueueTimerType = Type.GetType("System.Threading.TimerQueueTimer");
            s_timerQueueTimerTimerCallbackField = s_timerQueueTimerType?.GetField("m_timerCallback", BindingFlags.NonPublic | BindingFlags.Instance);
            s_timerQueueType = Type.GetType("System.Threading.TimerQueue");
            s_timerQueueFireQueuedTimerCompletionMethod = s_timerQueueType?.GetMethod("FireQueuedTimerCompletion", BindingFlags.NonPublic | BindingFlags.Static);
            s_timerType = typeof(Timer);
            s_timerOnIntervalElapsedField = s_timerType.GetField("onIntervalElapsed", BindingFlags.NonPublic | BindingFlags.Instance);
            WorksInThisRuntime = true;
        }
        catch (Exception ex)
        {
            Log.Publish(MessageLevel.Error, MessageFlags.BugReport, "Error in constructor", null, null, ex);
        }
    }

    /// <summary>
    /// Traces all queued items on the ThreadPool.
    /// </summary>
    /// <param name="sb"></param>
    public static void GetTrace(StringBuilder sb)
    {
        if (!WorksInThisRuntime)
        {
            sb.AppendLine("Not Supported");
            return;
        }

        try
        {
            foreach (object? iThreadPoolWorkItem in (IEnumerable)s_threadPoolGetQueuedWorkItemsMethod!.Invoke(null, null)!)
                ProcessIThreadPoolWorkItem(sb, iThreadPoolWorkItem);

        }
        catch (Exception ex)
        {
            Log.Publish(MessageLevel.Error, MessageFlags.BugReport, "Error in GetTrace", null, null, ex);

            WorksInThisRuntime = false;
        }

    }

    private static void ProcessIThreadPoolWorkItem(StringBuilder sb, object? item)
    {
        if (item is not null && item.GetType() == s_queueWorkerCallbackType)
        {
            WaitCallback callback = (WaitCallback)s_callbackField!.GetValue(item)!;
            object? state = s_stateField!.GetValue(item);

            if (!TryProcessThreadingTimerCallback(sb, callback, state))
                sb.AppendLine(callback.Method.GetFriendlyMethodNameWithClass());
        }
        else if (item is not null)
        {
            sb.AppendLine(item.GetType().ToString());
        }
    }

    private static bool TryProcessThreadingTimerCallback(StringBuilder sb, WaitCallback callback, object? state)
    {
        if (callback.Target is not null || callback.Method != s_timerQueueFireQueuedTimerCompletionMethod ||
            state is null || state.GetType() != s_timerQueueTimerType) return false;

        TimerCallback timerCallback = (TimerCallback)s_timerQueueTimerTimerCallbackField!.GetValue(state)!;

        if (!TryProcessTimersTimerCallback(sb, timerCallback))
            sb.AppendLine(timerCallback.Method.GetFriendlyMethodNameWithClass());

        return true;
    }

    private static bool TryProcessTimersTimerCallback(StringBuilder sb, TimerCallback timerCallback)
    {
        if (timerCallback.Target is null || timerCallback.Target.GetType() != s_timerType)
            return false;

        ElapsedEventHandler onIntervalElapsed = (ElapsedEventHandler)s_timerOnIntervalElapsedField!.GetValue(timerCallback.Target as Timer)!;
        sb.AppendLine(onIntervalElapsed.Method.GetFriendlyMethodNameWithClass());

        return true;
    }
}
