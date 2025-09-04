//******************************************************************************************************
//  ChildProcessManager.cs - Gbtc
//
//  Copyright © 2018, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may not use this
//  file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  02/05/2018 - J. Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Gemstone.Diagnostics;

/// <summary>
/// Represents a manager for automatically terminating child processes.
/// </summary>
public sealed partial class ChildProcessManager : IDisposable
{
    #region [ Members ]

    // Nested Types

    #region [ Library Import Types ]

    // ReSharper disable FieldCanBeMadeReadOnly.Local
    // ReSharper disable UnusedMember.Local
    // ReSharper disable InconsistentNaming
    // ReSharper disable MemberCanBePrivate.Local
    // ReSharper disable IdentifierTypo
    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    private enum JobObjectInfoType
    {
        AssociateCompletionPortInformation = 7,
        BasicLimitInformation = 2,
        BasicUIRestrictions = 4,
        EndOfJobTimeInformation = 6,
        ExtendedLimitInformation = 9,
        SecurityLimitInformation = 5,
        GroupInformation = 11
    }
    // ReSharper restore FieldCanBeMadeReadOnly.Local
    // ReSharper restore UnusedMember.Local
    // ReSharper restore InconsistentNaming
    // ReSharper restore MemberCanBePrivate.Local
    // ReSharper restore IdentifierTypo

    private sealed class SafeJobHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeJobHandle(nint handle) : base(true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            return CloseHandle(handle);
        }
    }

    #endregion

    // Events

    /// <summary>
    /// Raised when there is an exception while attempting to terminate child process.
    /// </summary>
    /// <remarks>
    /// This is currently only raised on non-Windows operating systems.
    /// </remarks>
    public event EventHandler<EventArgs<Exception>>? TerminationException;

    // Fields

    // On non-Windows operating systems we just track associated processes
    private readonly List<WeakReference<Process>> m_childProcesses = [];

    private SafeJobHandle? m_jobHandle;
    private bool m_disposed;

    #endregion

    #region [ Constructors ]

    /// <summary>
    /// Creates a new <see cref="ChildProcessManager"/>.
    /// </summary>
    public ChildProcessManager()
    {
        if (!OperatingSystem.IsWindows())
            return;

        // Let safe handle manage terminations on Windows
        GC.SuppressFinalize(this);

        // On Windows we add child processes to a job object such that when the job
        // is terminated, so are the child processes. Since safe handle ensures proper
        // closing of job handle, child processes will be terminated even if parent 
        // process is abnormally terminated
        m_jobHandle = new SafeJobHandle(CreateJobObject(nint.Zero, null!));

        JOBOBJECT_BASIC_LIMIT_INFORMATION info = new()
        {
            LimitFlags = 0x2000
        };

        JOBOBJECT_EXTENDED_LIMIT_INFORMATION extendedInfo = new()
        {
            BasicLimitInformation = info
        };

        int length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        nint extendedInfoPtr = Marshal.AllocHGlobal(length);

        Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);

        if (!SetInformationJobObject(m_jobHandle, JobObjectInfoType.ExtendedLimitInformation, extendedInfoPtr, (uint)length))
            throw new InvalidOperationException($"Unable to set information for ChildProcessManager job. Error: {Marshal.GetLastWin32Error()}");
    }

    /// <summary>
    /// Make sure child processes get disposed.
    /// </summary>
    ~ChildProcessManager()
    {
        Dispose();
    }

    #endregion

    #region [ Methods ]

    /// <summary>
    /// Releases all the resources used by the <see cref="ChildProcessManager"/> object.
    /// </summary>
    public void Dispose()
    {
        if (m_disposed)
            return;

        try
        {
            if (!OperatingSystem.IsWindows())
            {
                foreach (WeakReference<Process> childProcessReference in m_childProcesses)
                {
                    if (!childProcessReference.TryGetTarget(out Process? childProcess))
                        continue;

                    try
                    {
                        childProcess.Kill();
                    }
                    catch (Exception ex)
                    {
                        TerminationException?.Invoke(this, new EventArgs<Exception>(ex));
                    }
                }
            }

            m_jobHandle?.Dispose();
            m_jobHandle = null;
        }
        finally
        {
            m_disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Associates the specified <paramref name="process"/> as a child of this <see cref="ChildProcessManager"/> instance.
    /// </summary>
    /// <param name="process">The <see cref="Process"/> to associate.</param>
    /// <remarks>
    /// <para>
    /// The <paramref name="process"/> will be managed as an associated process of this <see cref="ChildProcessManager"/>
    /// instance. When this <see cref="ChildProcessManager"/> instance is disposed or garbage collected, the children
    /// processes will be terminated.
    /// </para>
    /// <para>
    /// Creating an instance of this class with lifetime scope of the executing application will cause any child processes
    /// to be terminated when the parent process shuts down, on Windows environments this will happen even when the parent
    /// process termination is abnormal.
    /// </para>
    /// </remarks>
    public void AddProcess(Process process)
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);

        if (Common.IsPosixEnvironment)
        {
            m_childProcesses.Add(new WeakReference<Process>(process));
        }
        else
        {
            if (m_jobHandle is not null && !AssignProcessToJobObject(m_jobHandle, process.SafeHandle))
                throw new InvalidOperationException($"Unable to add process to ChildProcessManager job. Error: {Marshal.GetLastWin32Error()}");
        }
    }

    #endregion

    #region [ Static ]

    // Static Methods

    // ReSharper disable InconsistentNaming
    [LibraryImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateJobObject(nint lpJobAttributes, string? lpName);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetInformationJobObject(SafeJobHandle hJob, JobObjectInfoType infoClass, nint lpJobObjectInfo, uint cbJobObjectInfoLength);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AssignProcessToJobObject(SafeJobHandle hJob, SafeProcessHandle hProcess);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint hObject);
    // ReSharper restore InconsistentNaming

    #endregion
}
