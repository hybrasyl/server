/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2020 ERISCO, LLC 
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */

using System;
using System.Collections.Generic;
using Hybrasyl.Xml.Objects;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.IO;
using Hybrasyl.Enums;
namespace Hybrasyl;


public class HybrasylLogger
{
    public ILogger Logger { get; set; }
    public LoggingLevelSwitch Level { get; set; }
}

/// <summary>
///     A wrapper class that provides an abstracted interface to Serilog
/// </summary>
public static class GameLog
{
    public static string DataDirectory { get; set; }
    public static Dictionary<LogType, HybrasylLogger> Loggers { get; set; } = new();

    public static void SetLevel(LogType logType, LogEventLevel level)
    {
        if (Loggers.TryGetValue(logType, out var logger))
        {
            logger.Level.MinimumLevel = level;
        }
    }

    public static LogEventLevel ConvertLevel(LogLevel level) =>
        level switch
        {
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Fatal => LogEventLevel.Fatal,
            LogLevel.Info => LogEventLevel.Information,
            LogLevel.Warn => LogEventLevel.Warning,
            LogLevel.All => LogEventLevel.Verbose,
            _ => LogEventLevel.Information
        };

    public static HybrasylLogger GetLogger(LogType type) =>
        Loggers.TryGetValue(type, out var ret) ? ret : Loggers[LogType.General];

    public static void SetLogLevel(LogType type, LogLevel level)
    {
        if (Loggers.TryGetValue(type, out var logger))
            logger.Level.MinimumLevel = ConvertLevel(level);
    }

    public static void Initialize(string dataDirectory, List<LogConfig> configs)
    {
        foreach (var config in configs)
        {
            var levelSwitch = new LoggingLevelSwitch();
            var path = string.IsNullOrEmpty(config.Destination) ? $"{config.Type}.log" : config.Destination;

            var loggerConfig = new LoggerConfiguration().MinimumLevel.ControlledBy(levelSwitch).Enrich.WithThreadId()
                .Enrich
                .WithExceptionData().WriteTo.File($"{Path.Combine(dataDirectory, path)}",
                    rollingInterval: RollingInterval.Day, retainedFileCountLimit: 90, rollOnFileSizeLimit: true);
            if (config.Type == LogType.General)
                loggerConfig = loggerConfig.WriteTo.Console();
            Loggers.Add(config.Type, new  HybrasylLogger { Logger = loggerConfig.CreateLogger(), Level = levelSwitch });
            Serilog.Log.Information($"Logger: added {config.Type} -> {path}");
        }
        // Ensure there is always a general logger and that it is "attached" to Serilog
        if (!Loggers.ContainsKey(LogType.General))
        {
            var generalSwitch = new LoggingLevelSwitch();

            var generalLog = new LoggerConfiguration().MinimumLevel.ControlledBy(generalSwitch).Enrich.WithThreadId()
                .Enrich
                .WithExceptionData().WriteTo
                .Map("LogType", "General",
                    configure: (name, wt) => wt.File($"{Path.Combine(dataDirectory, "logs")}/{name}-.log",
                        rollingInterval: RollingInterval.Day, retainedFileCountLimit: 90, rollOnFileSizeLimit: true))
                .WriteTo.Console()
                .CreateLogger();
            Loggers.Add(LogType.General, new HybrasylLogger { Logger = generalLog, Level = generalSwitch });
            GameLog.Info($"Logger: added General log");
        }

        Serilog.Log.Logger = Loggers[LogType.General].Logger;
    }

    public static void Log(LogEventLevel level = LogEventLevel.Information, LogType logType = LogType.General,
        string messageTemplate = "", params object[] propertyValues)
    {

        var logger = GetLogger(logType).Logger;

        switch (level)
        {
            case LogEventLevel.Debug:
                logger.Debug(messageTemplate, propertyValues);
                break;
            case LogEventLevel.Error:
                logger.Error(messageTemplate, propertyValues);
                break;
            case LogEventLevel.Fatal:
                logger.Fatal(messageTemplate, propertyValues);
                break;
            case LogEventLevel.Information: 
            default:
                logger.Information(messageTemplate, propertyValues);
                break;
            case LogEventLevel.Verbose:
                logger.Verbose(messageTemplate, propertyValues);
                break;
            case LogEventLevel.Warning:
                logger.Warning(messageTemplate, propertyValues);
                break;
        }
    }

    public static void LogWithException(Exception ex, LogEventLevel level = LogEventLevel.Error,
        LogType logType = LogType.General, string messageTemplate = "", params object[] propertyValues)
    {
        var logger = GetLogger(logType).Logger;

        switch (level)
        {
            case LogEventLevel.Debug:
                logger.Debug(ex, messageTemplate, propertyValues);
                break;
            case LogEventLevel.Error:
                logger.Error(ex, messageTemplate, propertyValues);
                break;
            case LogEventLevel.Fatal:
                logger.Fatal(ex, messageTemplate, propertyValues);
                break;
            case LogEventLevel.Information:
            default:
                logger.Information(ex, messageTemplate, propertyValues);
                break;
            case LogEventLevel.Verbose:
                logger.Verbose(ex, messageTemplate, propertyValues);
                break;
            case LogEventLevel.Warning:
                logger.Warning(ex, messageTemplate, propertyValues);
                break;
        }
    }

    // Provide easy to use shims here which are drop in replacements for log4net usage

    // Base "general" logs
    public static void Error(Exception ex, string messageTemplate = "", params object[] propertyValues)
    {
        LogWithException(ex, LogEventLevel.Error, LogType.General, messageTemplate, propertyValues);
    }

    public static void Info(Exception ex, string messageTemplate = "", params object[] propertyValues)
    {
        LogWithException(ex, LogEventLevel.Information, LogType.General, messageTemplate, propertyValues);
    }

    public static void Warning(Exception ex, string messageTemplate = "", params object[] propertyValues)
    {
        LogWithException(ex, LogEventLevel.Warning, LogType.General, messageTemplate, propertyValues);
    }

    public static void Debug(Exception ex, string messageTemplate = "", params object[] propertyValues)
    {
        LogWithException(ex, LogEventLevel.Error, LogType.General, messageTemplate, propertyValues);
    }

    public static void Error(string messageTemplate = "", params object[] propertyValues)
    {
        Log(LogEventLevel.Error, LogType.General, messageTemplate, propertyValues);
    }

    public static void Info(string messageTemplate = "", params object[] propertyValues)
    {
        Log(LogEventLevel.Information, LogType.General, messageTemplate, propertyValues);
    }

    public static void Warning(string messageTemplate = "", params object[] propertyValues)
    {
        Log(LogEventLevel.Warning, LogType.General, messageTemplate, propertyValues);
    }

    public static void Debug(string messageTemplate = "", params object[] propertyValues)
    {
        Log(LogEventLevel.Debug, LogType.General, messageTemplate, propertyValues);
    }

    public static void Fatal(string messageTemplate = "", params object[] propertyValues)
    {
        Log(LogEventLevel.Fatal, LogType.General, messageTemplate, propertyValues);
    }

    // log4net shims which need to be refactored
    public static void ErrorFormat(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Error, LogType.General, messageTemplate, propertyValues);
    }

    public static void InfoFormat(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Information, LogType.General, messageTemplate, propertyValues);
    }

    public static void WarningFormat(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Warning, LogType.General, messageTemplate, propertyValues);
    }

    public static void DebugFormat(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Debug, LogType.General, messageTemplate, propertyValues);
    }

    // User activity logs
    public static void UserActivityError(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Error, LogType.UserActivity, messageTemplate, propertyValues);
    }

    public static void UserActivityInfo(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Information, LogType.UserActivity, messageTemplate, propertyValues);
    }

    public static void UserActivityWarning(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Warning, LogType.UserActivity, messageTemplate, propertyValues);
    }

    public static void UserActivityDebug(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Debug, LogType.UserActivity, messageTemplate, propertyValues);
    }

    public static void UserActivityFatal(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Fatal, LogType.UserActivity, messageTemplate, propertyValues);
    }

    public static void UserActivityError(Exception ex, string messageTemplate, params object[] propertyValues)
    {
        LogWithException(ex, LogEventLevel.Error, LogType.UserActivity, messageTemplate, propertyValues);
    }

    public static void UserActivityInfo(Exception ex, string messageTemplate, params object[] propertyValues)
    {
        LogWithException(ex, LogEventLevel.Information, LogType.UserActivity, messageTemplate, propertyValues);
    }

    public static void UserActivityWarning(Exception ex, string messageTemplate, params object[] propertyValues)
    {
        LogWithException(ex, LogEventLevel.Warning, LogType.UserActivity, messageTemplate, propertyValues);
    }

    public static void UserActivityDebug(Exception ex, string messageTemplate, params object[] propertyValues)
    {
        LogWithException(ex, LogEventLevel.Debug, LogType.UserActivity, messageTemplate, propertyValues);
    }

    public static void UserActivityFatal(Exception ex, string messageTemplate, params object[] propertyValues)
    {
        LogWithException(ex, LogEventLevel.Fatal, LogType.UserActivity, messageTemplate, propertyValues);
    }

    // GM activity logs 
    public static void GmActivityError(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Error, LogType.GmActivity, messageTemplate, propertyValues);
    }

    public static void GmActivityInfo(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Information, LogType.GmActivity, messageTemplate, propertyValues);
    }

    public static void GmActivityWarning(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Warning, LogType.GmActivity, messageTemplate, propertyValues);
    }

    public static void GmActivityDebug(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Debug, LogType.GmActivity, messageTemplate, propertyValues);
    }

    public static void GmActivityFatal(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Fatal, LogType.GmActivity, messageTemplate, propertyValues);
    }

    public static void GmActivityError(Exception ex, string messageTemplate, params object[] propertyValues)
    {
        LogWithException(ex, LogEventLevel.Error, LogType.GmActivity, messageTemplate, propertyValues);
    }

    public static void GmActivityInfo(Exception ex, string messageTemplate, params object[] propertyValues)
    {
        LogWithException(ex, LogEventLevel.Information, LogType.GmActivity, messageTemplate, propertyValues);
    }

    public static void GmActivityWarning(Exception ex, string messageTemplate, params object[] propertyValues)
    {
        LogWithException(ex, LogEventLevel.Warning, LogType.GmActivity, messageTemplate, propertyValues);
    }

    public static void GmActivityDebug(Exception ex, string messageTemplate, params object[] propertyValues)
    {
        LogWithException(ex, LogEventLevel.Debug, LogType.GmActivity, messageTemplate, propertyValues);
    }

    public static void GmActivityFatal(Exception ex, string messageTemplate, params object[] propertyValues)
    {
        LogWithException(ex, LogEventLevel.Fatal, LogType.GmActivity, messageTemplate, propertyValues);
    }

    // Scripting activity logs
    public static void ScriptingError(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Error, LogType.Scripting, messageTemplate, propertyValues);
    }

    public static void ScriptingInfo(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Information, LogType.Scripting, messageTemplate, propertyValues);
    }

    public static void ScriptingWarning(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Warning, LogType.Scripting, messageTemplate, propertyValues);
    }

    public static void ScriptingDebug(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Debug, LogType.Scripting, messageTemplate, propertyValues);
    }

    public static void ScriptingFatal(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Fatal, LogType.Scripting, messageTemplate, propertyValues);
    }

    public static void ScriptingError(Exception ex, string messageTemplate, params object[] propertyValues)
    {
        LogWithException(ex, LogEventLevel.Error, LogType.Scripting, messageTemplate, propertyValues);
    }

    public static void ScriptingInfo(Exception ex, string messageTemplate, params object[] propertyValues)
    {
        LogWithException(ex, LogEventLevel.Information, LogType.Scripting, messageTemplate, propertyValues);
    }

    public static void ScriptingWarning(Exception ex, string messageTemplate, params object[] propertyValues)
    {
        LogWithException(ex, LogEventLevel.Warning, LogType.Scripting, messageTemplate, propertyValues);
    }

    public static void ScriptingDebug(Exception ex, string messageTemplate, params object[] propertyValues)
    {
        LogWithException(ex, LogEventLevel.Debug, LogType.Scripting, messageTemplate, propertyValues);
    }

    public static void ScriptingFatal(Exception ex, string messageTemplate, params object[] propertyValues)
    {
        LogWithException(ex, LogEventLevel.Fatal, LogType.Scripting, messageTemplate, propertyValues);
    }

    // Spawn activity logs
    public static void SpawnError(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Error, LogType.Spawn, messageTemplate, propertyValues);
    }

    public static void SpawnInfo(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Information, LogType.Spawn, messageTemplate, propertyValues);
    }

    public static void SpawnWarning(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Warning, LogType.Spawn, messageTemplate, propertyValues);
    }

    public static void SpawnDebug(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Debug, LogType.Spawn, messageTemplate, propertyValues);
    }

    public static void SpawnFatal(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Fatal, LogType.Spawn, messageTemplate, propertyValues);
    }

    public static void SpawnError(Exception ex, string messageTemplate, params object[] propertyValues)
    {
        LogWithException(ex, LogEventLevel.Error, LogType.Spawn, messageTemplate, propertyValues);
    }

    public static void SpawnInfo(Exception ex, string messageTemplate, params object[] propertyValues)
    {
        LogWithException(ex, LogEventLevel.Information, LogType.Spawn, messageTemplate, propertyValues);
    }

    public static void SpawnWarning(Exception ex, string messageTemplate, params object[] propertyValues)
    {
        LogWithException(ex, LogEventLevel.Warning, LogType.Spawn, messageTemplate, propertyValues);
    }

    public static void SpawnDebug(Exception ex, string messageTemplate, params object[] propertyValues)
    {
        LogWithException(ex, LogEventLevel.Debug, LogType.Spawn, messageTemplate, propertyValues);
    }

    public static void SpawnFatal(Exception ex, string messageTemplate, params object[] propertyValues)
    {
        LogWithException(ex, LogEventLevel.Fatal, LogType.Spawn, messageTemplate, propertyValues);
    }

    // Packet log
    public static void PacketInfo(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Information, LogType.Packet, messageTemplate, propertyValues);
    }

    // XML data load notices (errors / etc)
    public static void DataLogInfo(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Information, LogType.WorldData, messageTemplate, propertyValues);
    }

    public static void DataLogError(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Error, LogType.WorldData, messageTemplate, propertyValues);
    }

    public static void DataLogDebug(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Debug, LogType.WorldData, messageTemplate, propertyValues);
    }
}