//******************************************************************************************************
//  DiagnosticsLoggerOptions.cs - Gbtc
//
//  Copyright © 2026, Grid Protection Alliance.  All Rights Reserved.
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
//  10/13/2026 - Stephen C. Wills
//       Generated original version of source code.
//
//******************************************************************************************************

using Gemstone.Configuration;

namespace Gemstone.Diagnostics;

/// <summary>
/// Defines options for the <see cref="DiagnosticsLogger"/>.
/// </summary>
public class DiagnosticsLoggerOptions
{
    /// <summary>
    /// The default log path for the <see cref="DiagnosticsLogger"/>.
    /// </summary>
    public const string DefaultLogPath = "Logs";

    /// <summary>
    /// The default maximum number of log files for the <see cref="DiagnosticsLogger"/>.
    /// </summary>
    public const int DefaultMaxLogFiles = 300;

    /// <summary>
    /// The default expected message rate limit for the <see cref="DiagnosticsLogger"/>.
    /// </summary>
    public const int DefaultRateLimit = 100;

    /// <summary>
    /// The default burst limit for the <see cref="DiagnosticsLogger"/>.
    /// </summary>
    public const int DefaultBurstLimit = 5000;

    /// <summary>
    /// The default log verbosity for the <see cref="DiagnosticsLogger"/>.
    /// </summary>
    public const VerboseLevel DefaultLogVerbosity = VerboseLevel.Ultra;

    /// <summary>
    /// Gets or sets the path used to archive log files.
    /// </summary>
    public string LogPath { get; set; } = DefaultLogPath;

    /// <summary>
    /// Gets or sets the maximum number of log files to keep in the archive.
    /// </summary>
    public int MaxLogFiles { get; set; } = DefaultMaxLogFiles;

    /// <summary>
    /// Gets or sets the maximum expected rate at which debug messages are logged for rate limiting.
    /// </summary>
    public int DebugRate { get; set; } = DefaultRateLimit;

    /// <summary>
    /// Gets or sets the maximum expected rate at which information messages are logged for rate limiting.
    /// </summary>
    public int InformationRate { get; set; } = DefaultRateLimit;

    /// <summary>
    /// Gets or sets the maximum expected rate at which warning messages are logged for rate limiting.
    /// </summary>
    public int WarningRate { get; set; } = DefaultRateLimit;

    /// <summary>
    /// Gets or sets the maximum expected rate at which error messages are logged for rate limiting.
    /// </summary>
    public int ErrorRate { get; set; } = DefaultRateLimit;

    /// <summary>
    /// Gets or sets the maximum expected rate at which critical messages are logged for rate limiting.
    /// </summary>
    public int CriticalRate { get; set; } = DefaultRateLimit;

    /// <summary>
    /// Gets or sets the maximum number of debug messages that can be logged in a burst.
    /// </summary>
    public int DebugBurstLimit { get; set; } = DefaultBurstLimit;

    /// <summary>
    /// Gets or sets the maximum number of information messages that can be logged in a burst.
    /// </summary>
    public int InformationBurstLimit { get; set; } = DefaultBurstLimit;

    /// <summary>
    /// Gets or sets the maximum number of warning messages that can be logged in a burst.
    /// </summary>
    public int WarningBurstLimit { get; set; } = DefaultBurstLimit;

    /// <summary>
    /// Gets or sets the maximum number of error messages that can be logged in a burst.
    /// </summary>
    public int ErrorBurstLimit { get; set; } = DefaultBurstLimit;

    /// <summary>
    /// Gets or sets the maximum number of critical messages that can be logged in a burst.
    /// </summary>
    public int CriticalBurstLimit { get; set; } = DefaultBurstLimit;

    /// <summary>
    /// Gets or sets the verbosity level for logging.
    /// </summary>
    public VerboseLevel LogVerbosity { get; set; } = DefaultLogVerbosity;

    /// <summary>
    /// Loads logging settings from the default <see cref="Settings"/> instance.
    /// </summary>
    /// <param name="settingsCategory">The category name that defines the section containing logging settings.</param>
    /// <returns>Options used to initialize the <see cref="DiagnosticsLogger"/>.</returns>
    public static DiagnosticsLoggerOptions FromSettings(string? settingsCategory = null)
    {
        settingsCategory ??= DiagnosticsLogger.DefaultSettingsCategory;

        return Settings.Instance is not null
            ? FromSettings(Settings.Instance[settingsCategory])
            : new();
    }

    /// <summary>
    /// Loads logging settings from the default <see cref="Settings"/> instance.
    /// </summary>
    /// <param name="settingsSection">The section containing logging settings.</param>
    /// <returns>Options used to initialize the <see cref="DiagnosticsLogger"/>.</returns>
    public static DiagnosticsLoggerOptions FromSettings(SettingsSection settingsSection)
    {
        dynamic loggingSettings = settingsSection;

        return new()
        {
            LogPath = loggingSettings.LogPath,
            MaxLogFiles = loggingSettings.MaxLogFiles,

            DebugRate = loggingSettings.DebugRate,
            InformationRate = loggingSettings.InformationRate,
            WarningRate = loggingSettings.WarningRate,
            ErrorRate = loggingSettings.ErrorRate,
            CriticalRate = loggingSettings.CriticalRate,

            DebugBurstLimit = loggingSettings.DebugBurstLimit,
            InformationBurstLimit = loggingSettings.InformationBurstLimit,
            WarningBurstLimit = loggingSettings.WarningBurstLimit,
            ErrorBurstLimit = loggingSettings.ErrorBurstLimit,
            CriticalBurstLimit = loggingSettings.CriticalBurstLimit,

            LogVerbosity = loggingSettings.Verbosity
        };
    }
}
