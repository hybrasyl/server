using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Loaders;
using System;
using System.IO;

namespace Hybrasyl.Subsystems.Scripting;

/// <summary>
/// A Moonsharp script loader, used to enable "require" and "loadfile" in Lua
/// scripts to load includes (modules) from a blessed / safe path.
/// </summary>
/// <param name="includePath">The path to the allowed directory. </param>
public class WorldModuleLoader(string basePath, string modulePath) : ScriptLoaderBase
{
    public string ModulePath { get; } = modulePath;
    public string BasePath { get; } = basePath;

    private string GetFullPathname(string moduleName) =>
        Path.Join(Path.Join(BasePath, ModulePath), $"{moduleName}.lua");

    /// <summary>
    /// World module loader for "require" in Lua.
    /// </summary>
    /// <param name="modname">The module name passed from the script.</param>
    /// <param name="globalContext">The script's global context</param>
    /// <returns></returns>
    public override string ResolveModuleName(string modname, Table globalContext)
    {
        if (string.IsNullOrEmpty(modname)) throw new ArgumentNullException(nameof(modname));

        var pathName = GetFullPathname(modname);

        if (Path.Exists(pathName)) return pathName;

        throw new ArgumentException($"{nameof(ResolveModuleName)}: module {modname} does not exist");

    }

    /// <summary>
    /// World module handler for "loadfile" in Lua. We disallow this usage except for module loading.
    /// </summary>
    /// <param name="file">The file name to load.</param>
    /// <param name="globalContext">The script's global context.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public override object LoadFile(string file, Table globalContext)
    {
        var d2 = new DirectoryInfo(file);
        var d1 = new DirectoryInfo(Path.Join(BasePath, ModulePath));
        var isParent = false;
        while (d2.Parent != null)
        {
            if (d2.Parent.FullName == d1.FullName) { isParent = true; break; }
            d2 = d2.Parent;
        }

        if (!isParent) throw new ArgumentException($"{nameof(LoadFile)}: module path loading outside of {d2.FullName} is prohibited");
        return File.ReadAllText(file);
    }

    public override bool ScriptFileExists(string name)
    {
        throw new NotImplementedException("Use require instead");
    }
}