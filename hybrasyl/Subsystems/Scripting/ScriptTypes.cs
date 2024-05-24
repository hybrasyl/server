// This file is part of Project Hybrasyl.
// 
// This program is free software; you can redistribute it and/or modify
// it under the terms of the Affero General Public License as published by
// the Free Software Foundation, version 3.
// 
// This program is distributed in the hope that it will be useful, but
// without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
// for more details.
// 
// You should have received a copy of the Affero General Public License along
// with this program. If not, see <http://www.gnu.org/licenses/>.
// 
// (C) 2020-2023 ERISCO, LLC
// 
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using System;
using System.Collections.Generic;
using Hybrasyl.Interfaces;
using Hybrasyl.Objects;
using MoonSharp.Interpreter;

namespace Hybrasyl.Subsystems.Scripting;

public enum ScriptResult
{
    Success,
    Failure,
    NoExecution,
    Disabled,
    FunctionMissing,
    ScriptMissing
}

public enum ScriptErrorType
{
    SyntaxError,
    DynamicExpressionError,
    InternalError,
    RuntimeError,
    CSharpError,
    Unknown
}

public class ScriptExecutionResult
{
    public ScriptExecutionResult()
    {
        ExecutionTime = DateTime.Now;
        Guid = Guid.NewGuid();
    }

    public ScriptResult Result { get; set; }
    public DynValue Return { get; set; }
    public string ExecutedExpression { get; set; }
    public string Location { get; set; }

    public DateTime ExecutionTime { get; set; }

    public ScriptExecutionError Error { get; set; }
    public Guid Guid { get; set; }

    public static ScriptExecutionResult Disabled =>
        new() { Result = ScriptResult.Disabled, Return = DynValue.Nil, Error = null };

    public static ScriptExecutionResult NotFound => new()
    {
        Result = ScriptResult.NoExecution,
        Return = DynValue.Nil,
        Error = null
    };

    public static ScriptExecutionResult NoExecution => new()
    {
        Result = ScriptResult.NoExecution,
        Return = DynValue.Nil,
        Error = null
    };
}

public class ScriptExecutionError
{
    public ScriptErrorType ErrorType { get; set; }
    public string HumanizedError { get; set; }
    public Exception ScriptException { get; set; }
    public string Filename { get; set; }
    public int LineNumber { get; set; }
    public string Error { get; set; }

    public override string ToString() => $"{ErrorType}: {Filename} line {LineNumber}\n{Error}\n\n{HumanizedError}";
}

public class DialogInvocation
{
    public DialogInvocation(IInteractable origin, User target, IWorldObject source)
    {
        Source = source;
        Origin = origin;
        Target = target;
        Environment = new ScriptEnvironment();
    }

    public ScriptEnvironment Environment { get; set; }

    // Origin is the script responsible for what is currently happening
    public IInteractable Origin { get; set; }

    public Script Script => Origin?.Script;

    // Target is the target of a (dialog) spell, the recipient of a dialog, etc. Somewhat obviously,
    // mundanes cannot be shown dialogs
    public User Target { get; set; }

    // Source is the object responsible for the action occurring - could be a user, a mundane, etc
    public IWorldObject Source { get; set; }
    public ushort Sprite => Origin.Sprite;

    public ScriptExecutionResult ExecuteExpression(string expr)
    {
        Environment.Add("origin", Origin);
        Environment.Add("source", Source);
        Environment.Add("target", Target);
        return Script.ExecuteExpression(expr, Environment);
    }

    public ScriptExecutionResult ExecuteFunction(string function)
    {
        Environment.Add("origin", Origin);
        Environment.Add("source", Source);
        Environment.Add("target", Target);
        return Script.ExecuteFunction(function, Environment);
    }
}

public class ScriptEnvironment
{
    public ScriptEnvironment()
    {
        Variables = new Dictionary<string, dynamic>();
    }

    public Dictionary<string, dynamic> Variables { get; set; }
    public string DialogPath { get; set; }

    public void Add(string name, dynamic obj)
    {
        Variables[name] = obj;
    }

    public static ScriptEnvironment Create(params (string name, dynamic obj)[] variables)
    {
        var ret = new ScriptEnvironment();
        foreach (var v in variables) ret.Add(v.name, v.obj);
        return ret;
    }

    // TODO: clarify this terminology in scripting
    public static ScriptEnvironment CreateWithTarget(dynamic target) => Create(("target", target));

    public static ScriptEnvironment CreateWithTargetAndSource(dynamic target, dynamic source) =>
        Create(("target", target), ("source", source));

    public static ScriptEnvironment CreateWithOriginAndTarget(dynamic origin, dynamic target) =>
        Create(("origin", origin), ("target", target));

    public static ScriptEnvironment CreateWithOriginTargetAndSource(dynamic origin, dynamic target, dynamic source) =>
        Create(("origin", origin), ("target", target), ("source", source));
}