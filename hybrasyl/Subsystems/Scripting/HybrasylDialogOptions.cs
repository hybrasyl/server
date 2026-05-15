using Hybrasyl.Subsystems.Dialogs;
using MoonSharp.Interpreter;
using System;
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
            throw new ArgumentException($"{nameof(AddOption)}: option or luaExpr argument was null or empty");

        Options.Add(option, new DialogOption
        {
            CallbackFunction = luaExpr,
            CheckExpression = checkExpr,
            OptionText = option
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
            throw new ArgumentException($"{nameof(AddOption)}: option or nextDialog argument was null or empty");

        if (nextDialog.Dialog is JumpDialog j)
            Options.Add(option, new DialogOption
            {
                JumpDialog = j,
                CheckExpression = checkExpr,
                OptionText = option
            });
        else
            throw new ArgumentException($"{nameof(AddOption)}: Dialog option {option}: nextDialog argument must be JumpDialog, but was a {nextDialog.DialogType.Name}");
    }

    /// <summary>
    ///     Add a dialog option that will start a new sequence when selected by a player.
    /// </summary>
    /// <param name="option">The option text</param>
    /// <param name="sequence">The DialogSequence that wil be started when the option is selected by a player</param>
    /// <param name="checkExpr">A lua expression returning a boolean which controls whether this option is displayed to the player</param>
    public void AddOption(string option, HybrasylDialogSequence sequence, string checkExpr = null)
    {
        if (string.IsNullOrEmpty(option) || sequence is null)
            throw new ArgumentException($"{nameof(AddOption)}: option or sequence argument was null or empty");

        Options.Add(option, new DialogOption
        {
            OverrideSequence = sequence.Sequence,
            CheckExpression = checkExpr,
            OptionText = option
        });

    }
}