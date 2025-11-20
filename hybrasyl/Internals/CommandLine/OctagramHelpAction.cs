using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.CommandLine.Invocation;
using System.IO;
using System.Resources;
using System.Reflection;

namespace Hybrasyl.Internals.CommandLine;

public class OctagramHelpAction : SynchronousCommandLineAction
{
    private readonly HelpAction _octagram;

    public OctagramHelpAction(HelpAction action) => _octagram = action;

    public override int Invoke(ParseResult parseResult)
    {
        int result;
        var assembly = Assembly.GetExecutingAssembly();
        var octagram = assembly.GetManifestResourceStream("Hybrasyl.Resources.octagram.txt");

        if (octagram is null)
        {
            result = _octagram.Invoke(parseResult);
            return result;
        }

        using var reader = new StreamReader(octagram);

        var text = reader.ReadToEnd();
        Console.WriteLine(text);
        Console.WriteLine();
        result = _octagram.Invoke(parseResult);
        Console.WriteLine("Any parameter above can also be set by environment variable as indicated (eg $HYB_DATA_DIR would set the value of --dataDir).");
        Console.WriteLine();
        return result;
    }
}