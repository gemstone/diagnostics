//******************************************************************************************************
//  DiagnosticLogger.cs - Gbtc
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

using System;
using System.IO;
using System.Linq;
using Gemstone.Configuration;
using Gemstone.IO;
using Microsoft.Extensions.Logging;

namespace Gemstone.Diagnostics;

/// <summary>
/// A logger that writes messages to the <see cref="Logger"/>.
/// </summary>
public sealed class DiagnosticsLogger : ILogger, IDefineSettings
{
    /// <summary>
    /// The default settings category for the <see cref="DiagnosticsLogger"/>.
    /// </summary>
    public const string DefaultSettingsCategory = "Diagnostics";

    // An empty scope without any logic
    private sealed class EmptyScope : IDisposable
    {
        public static EmptyScope Instance { get; } = new();

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }

    private LogEventPublisher? m_logDebug;
    private LogEventPublisher? m_logInformation;
    private LogEventPublisher? m_logWarning;
    private LogEventPublisher? m_logError;
    private LogEventPublisher? m_logCritical;

    /// <summary>
    /// Gets the <see cref="LogPublisher"/> for the <see cref="DiagnosticsLogger"/>.
    /// </summary>
    public LogPublisher LogPublisher { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticsLogger"/> class.
    /// </summary>
    /// <param name="categoryName">The category of log messages captured by this instance of the <see cref="DiagnosticsLogger"/> class.</param>
    public DiagnosticsLogger(string? categoryName = null)
    {
        Type loggerType = GetLoggerType(categoryName);
        LogPublisher = Logger.CreatePublisher(loggerType, MessageClass.Framework);
    }

    /// <summary>
    /// Initializes the <see cref="DiagnosticsLogger"/>.
    /// </summary>
    public void Initialize(DiagnosticsLoggerOptions? options = null)
    {
        options ??= DiagnosticsLoggerOptions.FromSettings();

        // Retrieve application log path as defined in the config file
        string logPath = FilePath.GetAbsolutePath(options.LogPath);

        // Make sure log directory exists
        try
        {
            if (!Directory.Exists(logPath))
                Directory.CreateDirectory(logPath);
        }
        catch (Exception ex)
        {
            // Attempt to default back to common log file path
            if (!Directory.Exists(DiagnosticsLoggerOptions.DefaultLogPath))
            {
                try
                {
                    Directory.CreateDirectory(DiagnosticsLoggerOptions.DefaultLogPath);
                }
                catch (Exception ex2)
                {
                    Logger.SwallowException(ex2, $"Failed to create configured log path \"{logPath}\" or default log path \"{DiagnosticsLoggerOptions.DefaultLogPath}\": {ex.Message}");
                }
            }

            LogPublisher.Publish(MessageLevel.Critical, MessageFlags.UsageIssue, "Log Folder Creation", $"Failed to create logging directory \"{logPath}\" due to exception, defaulting to \"{DiagnosticsLoggerOptions.DefaultLogPath}\": {ex.Message}");
            logPath = DiagnosticsLoggerOptions.DefaultLogPath;
        }

        // Initialize log file writer
        try
        {
            Logger.FileWriter.SetPath(logPath);
            Logger.FileWriter.SetLoggingFileCount(options.MaxLogFiles);
            Logger.FileWriter.Verbose = options.LogVerbosity;
        }
        catch (Exception ex)
        {
            Logger.SwallowException(ex, $"Failed to initialize Logger.FileWriter: {ex.Message}");
        }

        m_logDebug = LogPublisher.RegisterEvent(MessageLevel.Debug, MessageFlags.None, "Debug Event", 0, MessageRate.PerSecond(options.DebugRate), options.DebugBurstLimit);
        m_logInformation = LogPublisher.RegisterEvent(MessageLevel.Info, MessageFlags.None, "Information Event", 0, MessageRate.PerSecond(options.InformationRate), options.InformationBurstLimit);
        m_logWarning = LogPublisher.RegisterEvent(MessageLevel.Warning, MessageFlags.None, "Warning Event", 0, MessageRate.PerSecond(options.WarningRate), options.WarningBurstLimit);
        m_logError = LogPublisher.RegisterEvent(MessageLevel.Error, MessageFlags.None, "Error Event", 0, MessageRate.PerSecond(options.ErrorRate), options.ErrorBurstLimit);
        m_logCritical = LogPublisher.RegisterEvent(MessageLevel.Critical, MessageFlags.None, "Critical Event", 0, MessageRate.PerSecond(options.CriticalRate), options.CriticalBurstLimit);

        LogPublisher.InitialStackMessages = LogStackMessages.Empty;
    }

    /// <inheritdoc />
    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return EmptyScope.Instance;
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None;
    }

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        if (formatter is null)
            throw new ArgumentNullException(nameof(formatter));

        string message = formatter(state, exception);

        if (string.IsNullOrEmpty(message))
            return;

        string? details;

        if (string.IsNullOrWhiteSpace(eventId.Name) && eventId.Id == 0)
        {
            details = null;
        }
        else
        {
            details = string.IsNullOrWhiteSpace(eventId.Name) ? $"[{eventId.Id}]" : $"{eventId.Name} [{eventId.Id}]";

            if (Logger.FileWriter.Verbose >= VerboseLevel.Ultra)
                details += $" for {state?.GetType().Name ?? "undefined type"}";
        }

        switch (logLevel)
        {
            case LogLevel.Trace:
            case LogLevel.Debug:
                m_logDebug?.Publish(message, details, exception);
                break;
            case LogLevel.Information:
                m_logInformation?.Publish(message, details, exception);
                break;
            case LogLevel.Warning:
                m_logWarning?.Publish(message, details, exception);
                break;
            case LogLevel.Error:
                m_logError?.Publish(message, details, exception);
                break;
            case LogLevel.Critical:
                m_logCritical?.Publish(message, details, exception);
                break;
        }
    }

    /// <inheritdoc cref="IDefineSettings.DefineSettings" />
    public static void DefineSettings(Settings settings, string settingsCategory = DefaultSettingsCategory)
    {
        dynamic loggingSettings = settings[settingsCategory];

        loggingSettings.LogPath = (DiagnosticsLoggerOptions.DefaultLogPath, "Defines the path used to archive log files.");
        loggingSettings.MaxLogFiles = (DiagnosticsLoggerOptions.DefaultMaxLogFiles, "Defines the maximum number of log files to keep in the archive.");

        loggingSettings.DebugRate = (DiagnosticsLoggerOptions.DefaultRateLimit, "Defines the maximum expected rate at which debug messages are logged for rate limiting.");
        loggingSettings.InformationRate = (DiagnosticsLoggerOptions.DefaultRateLimit, "Defines the maximum expected rate at which information messages are logged for rate limiting.");
        loggingSettings.WarningRate = (DiagnosticsLoggerOptions.DefaultRateLimit, "Defines the maximum expected rate at which warning messages are logged for rate limiting.");
        loggingSettings.ErrorRate = (DiagnosticsLoggerOptions.DefaultRateLimit, "Defines the maximum expected rate at which error messages are logged for rate limiting.");
        loggingSettings.CriticalRate = (DiagnosticsLoggerOptions.DefaultRateLimit, "Defines the maximum expected rate at which critical messages are logged for rate limiting.");

        loggingSettings.DebugBurstLimit = (DiagnosticsLoggerOptions.DefaultBurstLimit, "Defines the maximum number of debug messages that can be logged in a burst.");
        loggingSettings.InformationBurstLimit = (DiagnosticsLoggerOptions.DefaultBurstLimit, "Defines the maximum number of information messages that can be logged in a burst.");
        loggingSettings.WarningBurstLimit = (DiagnosticsLoggerOptions.DefaultBurstLimit, "Defines the maximum number of warning messages that can be logged in a burst.");
        loggingSettings.ErrorBurstLimit = (DiagnosticsLoggerOptions.DefaultBurstLimit, "Defines the maximum number of error messages that can be logged in a burst.");
        loggingSettings.CriticalBurstLimit = (DiagnosticsLoggerOptions.DefaultBurstLimit, "Defines the maximum number of critical messages that can be logged in a burst.");

        loggingSettings.Verbosity = (DiagnosticsLoggerOptions.DefaultLogVerbosity, "Defines the verbosity level for logging.");
    }

    private static Type GetLoggerType(string? categoryName)
    {
        if (categoryName is null)
            return typeof(DiagnosticsLogger);

        try
        {
            static Type? FindType(string typeName) => AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(typeName))
                .FirstOrDefault(type => type is not null);

            return Type.GetType(categoryName)
                ?? FindType(categoryName)
                ?? typeof(DiagnosticsLogger);
        }
        catch (Exception ex)
        {
            Logger.SwallowException(ex);
            return typeof(DiagnosticsLogger);
        }
    }
}
