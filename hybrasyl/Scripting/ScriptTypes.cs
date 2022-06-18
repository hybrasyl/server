using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Hybrasyl.Objects;
using Microsoft.DocAsCode.SubCommands;
using MoonSharp.Interpreter;

namespace Hybrasyl.Scripting
{
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
        public ScriptResult Result { get; set; }
        public DynValue Return { get; set; }
        public string ExecutedExpression { get; set; }
        public string Location { get; set; }

        public DateTime ExecutionTime { get; set; }

        public ScriptExecutionError Error { get; set; }
        public Guid Guid { get; set; }

        public ScriptExecutionResult()
        {
            ExecutionTime = DateTime.Now;
            Guid = Guid.NewGuid();
        }

        public static ScriptExecutionResult Disabled => new ScriptExecutionResult
            {Result = ScriptResult.Disabled, Return = DynValue.Nil, Error = null};

        public static ScriptExecutionResult NotFound => new ScriptExecutionResult
        {
            Result = ScriptResult.NoExecution,
            Return = DynValue.Nil,
            Error = null
        };

        public static ScriptExecutionResult NoExecution => new ScriptExecutionResult
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

        public override string ToString()
        {
            return $"{ErrorType}: {Filename} line {LineNumber}\n{Error}\n\n{HumanizedError}";
        }
    }

    public class ScriptEnvironment
    {
        public Dictionary<string, dynamic> Variables { get; set; }
        public string DialogPath { get; set; }

        public ScriptEnvironment()
        {
            Variables = new();
        }

        public void Add(string name, dynamic obj) =>
            Variables.Add(name, obj);

        public static ScriptEnvironment Create(params (string name, dynamic obj)[] variables)
        {
            var ret = new ScriptEnvironment();
            foreach (var v in variables)
            {
                ret.Add(v.name, v.obj);
            }
            return ret;
        }

        public static ScriptEnvironment CreateWithInvoker(dynamic invoker) => Create(("invoker", invoker));

        public static ScriptEnvironment CreateWithInvokerAndSource(dynamic invoker, dynamic source) =>
            Create(("invoker", invoker), ("source", source));


    }

}
