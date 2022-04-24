using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Hybrasyl.Objects;
using Microsoft.DocAsCode.SubCommands;
using MoonSharp.Interpreter;

namespace Hybrasyl.Scripting
{
    public enum ScriptResult
    {
        Success,
        NoExecution,
        Disabled
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

        public ScriptExecutionError Error { get; set; }

        public static ScriptExecutionResult Disabled => new ScriptExecutionResult
            {Result = ScriptResult.Disabled, Return = DynValue.Nil, Error = null};
    }

    public class ScriptExecutionError
    {
        public ScriptErrorType ErrorType { get; set; }
        public string HumanizedError { get; set; }
        public Exception ScriptException { get; set; }
        public string Filename { get; set; }
        public int LineNumber { get; set; }
        public string Error { get; set; }
    }

    public class ScriptEnvironment
    {
        public Dictionary<string, dynamic> Variables;
        public string Function;
    }

}
