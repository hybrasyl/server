using Hybrasyl.Internals.Logging;
using Hybrasyl.Subsystems.Dialogs;
using MoonSharp.Interpreter;
using System.Collections.Specialized;

namespace Hybrasyl.Subsystems.Scripting;

/// <summary>
///     A collection of dialog options that can be used by an options dialog (a dialog that displays a list of options for
///     a player to select).
/// </summary>
[MoonSharpUserData]
public class HybrasylDialogOptions
{
    public OrderedDictionary Options = new();

    /// <summary>
    ///     Add a dialog option which will fire a function when selected by a player.
    /// </summary>
    /// <param name="option">The option text</param>
    /// <param name="luaExpr">The lua expression to be evaluated when the option is selected by a player</param>
    /// <param name="checkExpr">A lua expression returning a boolean which controls whether this option is displayed to the player</param>
    public void AddOption(string option, string luaExpr = null, string checkExpr = null)
    {
        if (string.IsNullOrWhiteSpace(option) || string.IsNullOrWhiteSpace(luaExpr))
        {
            GameLog.ScriptingError(
                "AddOption: either option (first argument) or lua expression (second argument) was null or empty");
            return;
        }

        Options.Add(option, new DialogOption
        {
            CallbackFunction = luaExpr,
            CheckExpression = checkExpr
        });
    }

    /// <summary>
    ///     Add a dialog option which will fire a JumpDialog when selected by a player.
    /// </summary>
    /// <param name="option">The option text</param>
    /// <param name="nextDialog">The JumpDialog that will be used by this option</param>
    /// <param name="checkExpr">A lua expression returning a boolean which controls whether this option is displayed to the player</param>
    public void AddOption(string option, HybrasylDialog nextDialog, string checkExpr = null)
    {
        if (string.IsNullOrEmpty(option) || nextDialog is null)
        {
            GameLog.ScriptingError(
                "AddOption: for options set, option (first argument) or dialog (second argument) was null or empty");
            return;
        }

        if (nextDialog.Dialog is JumpDialog j)
            Options.Add(option, new DialogOption
            {
                JumpDialog = j,
                CheckExpression = checkExpr
            });
        else
            GameLog.ScriptingError(
                $"AddOption: Dialog option {option}: dialog must be JumpDialog, but was a {nextDialog.DialogType.Name}, ignored");
    }

    /// <summary>
    ///     Add a dialog option that will start a new sequence when selected by a player.
    /// </summary>
    /// <param name="option">The option text</param>
    /// <param name="sequence">The DialogSequence that wil be started when the option is selected by a player</param>
    /// <param name="checkExpr">A lua expression returning a boolean which controls whether this option is displayed to the player</param>
    public void AddOption(string option, HybrasylDialogSequence sequence, string checkExpr = null)
    {
        Options.Add(option, new DialogOption
        {
            OverrideSequence = sequence.Sequence,
            CheckExpression = checkExpr
        });

    }
}