//******************************************************************************************************
//  LoggerExtensions.cs - Gbtc
//
//  Copyright © 2024, Grid Protection Alliance.  All Rights Reserved.
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
//  04/07/2024 - Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Gemstone.Diagnostics;

/// <summary>
/// Defines extension methods related to logging.
/// </summary>
public static class LoggerExtensions
{
    /// <summary>
    /// Converts a <see cref="MessageLevel"/> to a <see cref="LogLevel"/>.
    /// </summary>
    /// <param name="level"><see cref="MessageLevel"/> to convert.</param>
    /// <returns><see cref="LogLevel"/> equivalent of the specified <see cref="MessageLevel"/>.</returns>
    /// <remarks>
    /// Note that <see cref="LogLevel.Trace"/> has no equivalent <see cref="MessageLevel"/>.
    /// </remarks>
    public static LogLevel ToLogLevel(this MessageLevel level) => level switch
    {
        MessageLevel.Critical => LogLevel.Critical,
        MessageLevel.Error => LogLevel.Error,
        MessageLevel.Warning => LogLevel.Warning,
        MessageLevel.Info => LogLevel.Information,
        MessageLevel.Debug => LogLevel.Debug,
        _ => LogLevel.None
    };

    /// <summary>
    /// Converts a <see cref="LogLevel"/> to a <see cref="MessageLevel"/>.
    /// </summary>
    /// <param name="level"><see cref="LogLevel"/> to convert.</param>
    /// <returns><see cref="MessageLevel"/> equivalent of the specified <see cref="LogLevel"/>.</returns>
    /// <remarks>
    /// Note that <see cref="LogLevel.Trace"/> has no equivalent <see cref="MessageLevel"/>.
    /// </remarks>
    public static MessageLevel ToMessageLevel(this LogLevel level) => level switch
    {
        LogLevel.Critical => MessageLevel.Critical,
        LogLevel.Error => MessageLevel.Error,
        LogLevel.Warning => MessageLevel.Warning,
        LogLevel.Information => MessageLevel.Info,
        LogLevel.Debug => MessageLevel.Debug,
        _ => MessageLevel.NA
    };

    /// <summary>
    /// Adds Gemstone <see cref="DiagnosticsLogger"/> to the factory.
    /// </summary>
    /// <param name="builder">The extension method argument.</param>
    public static ILoggingBuilder AddGemstoneDiagnostics(this ILoggingBuilder builder)
    {
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, DiagnosticsLoggerProvider>());

        return builder;
    }
}
