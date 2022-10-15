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
using Hybrasyl.Enums;
using Serilog.Core.Enrichers;
using Serilog.Events;

namespace Hybrasyl;

/// <summary>
///     A wrapper class that provides an abstracted interface to Serilog
/// </summary>
public static class GameLog
{
    public static void Log(LogEventLevel level = LogEventLevel.Information, LogType logType = LogType.General,
        string messageTemplate = "", params object[] propertyValues)
    {
        var logWithType = Serilog.Log.ForContext(new PropertyEnricher("LogType", logType.ToString()));

        switch (level)
        {
            case LogEventLevel.Debug:
                logWithType.Debug(messageTemplate, propertyValues);
                break;
            case LogEventLevel.Error:
                logWithType.Error(messageTemplate, propertyValues);
                break;
            case LogEventLevel.Fatal:
                logWithType.Fatal(messageTemplate, propertyValues);
                break;
            case LogEventLevel.Information:
                logWithType.Information(messageTemplate, propertyValues);
                break;
            case LogEventLevel.Verbose:
                logWithType.Verbose(messageTemplate, propertyValues);
                break;
            case LogEventLevel.Warning:
                logWithType.Warning(messageTemplate, propertyValues);
                break;
        }
    }

    public static void LogWithException(Exception ex, LogEventLevel level = LogEventLevel.Error,
        LogType logType = LogType.General, string messageTemplate = "", params object[] propertyValues)
    {
        var logWithType = Serilog.Log.ForContext(new PropertyEnricher("LogType", logType.ToString()));

        switch (level)
        {
            case LogEventLevel.Debug:
                logWithType.Debug(ex, messageTemplate, propertyValues);
                break;
            case LogEventLevel.Error:
                logWithType.Error(ex, messageTemplate, propertyValues);
                break;
            case LogEventLevel.Fatal:
                logWithType.Fatal(ex, messageTemplate, propertyValues);
                break;
            case LogEventLevel.Information:
                logWithType.Information(ex, messageTemplate, propertyValues);
                break;
            case LogEventLevel.Verbose:
                logWithType.Verbose(ex, messageTemplate, propertyValues);
                break;
            case LogEventLevel.Warning:
                logWithType.Warning(ex, messageTemplate, propertyValues);
                break;
        }
    }

    public static bool IsGeneralEvent(LogEvent le)
    {
        if (le.Properties.TryGetValue("LogType", out var value))
            return value.ToString().Trim('"') == LogType.General.ToString();
        return true;
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
        Log(LogEventLevel.Information, LogType.XmlData, messageTemplate, propertyValues);
    }

    public static void DataLogError(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Error, LogType.XmlData, messageTemplate, propertyValues);
    }

    public static void DataLogDebug(string messageTemplate, params object[] propertyValues)
    {
        Log(LogEventLevel.Debug, LogType.XmlData, messageTemplate, propertyValues);
    }
}