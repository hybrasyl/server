using Hybrasyl.Internals.Logging;
using MoonSharp.Interpreter;

namespace Hybrasyl.Subsystems.Scripting;

/// <summary>
///     A logging class that can be used by scripts natively in Lua.
/// </summary>
[MoonSharpUserData]
public class ScriptLogger(string name)
{
    public string ScriptName { get; set; } = name;

    public void Info(string message) => GameLog.ScriptingInfo($"script log: {ScriptName}: {message}");

    public void Error(string message) => GameLog.ScriptingError($"script log: {ScriptName}: {message}");
}