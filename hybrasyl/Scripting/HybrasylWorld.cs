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

using Hybrasyl.Dialogs;
using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;
using MoonSharp.Interpreter;
using System;
using System.Collections;
using System.Collections.Specialized;
using Creature = Hybrasyl.Xml.Objects.Creature;

namespace Hybrasyl.Scripting;

/// <summary>
///     A collection of dialog options that can be used by an options dialog (a dialog that displays a list of options for
///     a player to select).
/// </summary>
[MoonSharpUserData]
public class HybrasylDialogOptions
{
    public OrderedDictionary Options;

    public HybrasylDialogOptions()
    {
        Options = new OrderedDictionary();
    }

    /// <summary>
    ///     Add a dialog option which will fire a function when selected by a player.
    /// </summary>
    /// <param name="option">The option text</param>
    /// <param name="luaExpr">The lua expression to be evaluated when the option is selected by a player</param>
    public void AddOption(string option, string luaExpr = null)
    {
        if (string.IsNullOrEmpty(option) || string.IsNullOrEmpty(luaExpr))
            GameLog.ScriptingError(
                "AddOption: either option (first argument) or lua expression (second argument) was null or empty");
        Options.Add(option, luaExpr);
    }

    /// <summary>
    ///     Add a dialog option which will fire a JumpDialog when selected by a player.
    /// </summary>
    /// <param name="option">The option text</param>
    /// <param name="nextDialog">The JumpDialog that will be used by this option</param>
    public void AddOption(string option, HybrasylDialog nextDialog)
    {
        if (string.IsNullOrEmpty(option) || nextDialog is null)
            GameLog.ScriptingError(
                "AddOption: for options set, option (first argument) or dialog (second argument) was null or empty");
        if (nextDialog.DialogType == typeof(JumpDialog))
            Options.Add(option, nextDialog);
        else
            GameLog.ScriptingError(
                $"AddOption: Dialog option {option}: dialog must be JumpDialog, but was a {nextDialog.DialogType.Name}, ignored");
    }

    /// <summary>
    ///     Add a dialog option that will start a new sequence when selected by a player.
    /// </summary>
    /// <param name="option">The option text</param>
    /// <param name="sequence">The DialogSequence that wil be started when the option is selected by a player</param>
    public void AddOption(string option, HybrasylDialogSequence sequence)
    {
        Options.Add(option, sequence);
    }
}

/// <summary>
///     The world, as represented in Lua.
/// </summary>
[MoonSharpUserData]
public class HybrasylWorld
{
    public HybrasylWorld(World world)
    {
        World = world;
    }

    internal World World { get; set; }

    /// <summary>
    ///     Return the current in game year.
    /// </summary>
    public int CurrentInGameYear => HybrasylTime.CurrentYear;

    /// <summary>
    ///     Return the current in game age (e.g. Hybrasyl, or Danaan).
    /// </summary>
    public string CurrentInGameAge => HybrasylTime.CurrentAgeName;

    public string CurrentInGameSeason => HybrasylTime.CurrentSeason;

    /// <summary>
    ///     Write a message to the game (server) informational log.
    /// </summary>
    /// <param name="message">The message to be written</param>
    public void WriteLog(string message)
    {
        GameLog.Info(message);
    }

    /// <summary>
    ///     Return the current in-game time.
    /// </summary>
    /// <returns></returns>
    public HybrasylTime CurrentTime()
    {
        var ht = HybrasylTime.Now;
        return ht;
    }

    /// <summary>
    ///     Get a user object for the specified user (player) name.
    /// </summary>
    /// <param name="username">The user to be returned</param>
    /// <returns>HybrasylUser object for the given user, or nil, if the player is not logged in.</returns>
    public HybrasylUser GetUser(string username)
    {
        if (Game.World.TryGetActiveUser(username, out var user)) return new HybrasylUser(user);
        return null;
    }

    /// <summary>
    ///     Create a new dialog options container.
    /// </summary>
    /// <returns>A new DialogOptions container.</returns>
    public HybrasylDialogOptions NewDialogOptions() => new();

    /// <summary>
    ///     Create a new dialog sequence.
    /// </summary>
    /// <param name="sequenceName">The name of the new sequence.</param>
    /// <param name="list">An arbitrary collection of dialogs that will be made part of this sequence.</param>
    /// <returns>The constructed dialog sequence</returns>
    public HybrasylDialogSequence NewDialogSequence(string sequenceName, params object[] list)
    {
        if (string.IsNullOrEmpty(sequenceName))
        {
            GameLog.ScriptingError("NewDialogSequence: Sequence name (first argument) was null / empty");
            return null;
        }

        var dialogSequence = new HybrasylDialogSequence(sequenceName);
        foreach (var entry in list)
            if (entry is HybrasylDialog)
            {
                var newdialog = entry as HybrasylDialog;
                dialogSequence.AddDialog(newdialog);
                newdialog.Sequence = dialogSequence.Sequence;
            }
            else if (entry is not null)
            {
                GameLog.ScriptingError(
                    $"NewDialogSequence: Unknown argument of type {entry.GetType()} was passed for a dialog - ignored");
            }
            else
            {
                GameLog.ScriptingError("NewDialogSequence: null argument passed as dialog - ignored");
            }

        return dialogSequence;
    }

    /// <summary>
    ///     Create a new "simple" (text-only) dialog.
    /// </summary>
    /// <param name="displayText">The text that the dialog will display to the player.</param>
    /// <param name="callback">
    ///     A lua callback that can be associated with the dialog, and will be fired when the dialog is
    ///     shown to a player.
    /// </param>
    /// <returns>The constructed dialog</returns>
    public HybrasylDialog NewDialog(string displayText, string callback = null)
    {
        if (string.IsNullOrEmpty(displayText))
        {
            GameLog.ScriptingError("NewDialog: Sequence name (first argument) was null / empty");
            return null;
        }

        var dialog = new SimpleDialog(displayText);
        dialog.SetCallbackHandler(callback);
        return new HybrasylDialog(dialog);
    }

    /// <summary>
    ///     Create a new dialog sequence consisting of a bunch of simple text dialogs.
    /// </summary>
    /// <param name="sequenceName">The name of the constructed sequence.</param>
    /// <param name="textList">A string array of dialog lines that will be used to construct each dialog in the sequence.</param>
    /// <returns>The constructed dialog seqeunce</returns>
    public HybrasylDialogSequence NewSimpleDialogSequence(string sequenceName, params string[] textList)
    {
        if (string.IsNullOrEmpty(sequenceName))
        {
            GameLog.ScriptingError("NewSimpleDialogSequence: Sequence name (first argument) was null / empty");
            return null;
        }

        var sequence = new DialogSequence(sequenceName);
        foreach (var entry in textList)
        {
            if (string.IsNullOrEmpty(entry))
            {
                GameLog.ScriptingWarning("NewSimpleDialogSequence: encountered empty / null dialog text, ignoring");
                continue;
            }

            sequence.AddDialog(new SimpleDialog(entry));
        }

        return new HybrasylDialogSequence(sequence);
    }

    /// <summary>
    ///     Create a new dialog sequence consisting of a simple dialog and a jump to a new sequence. Useful
    ///     for a lot of dialogs where you need to display one dialog and go back to the main menu.
    /// </summary>
    /// <param name="simpleDialog">Text for the simple dialog.</param>
    /// <param name="jumpTarget">The new sequence to start after the user hits next on the simple dialog.</param>
    /// <param name="callback">An optional Lua callback expression that will be attached to the simple dialog.</param>
    /// <param name="name">An optional name to give the dialog sequence.</param>
    /// <returns>The constructed dialog sequence</returns>
    public HybrasylDialogSequence NewTextAndJumpDialog(string simpleDialog, string jumpTarget, string callback = "",
        string name = null)
    {
        DialogSequence sequence;
        if (string.IsNullOrEmpty(simpleDialog) || string.IsNullOrEmpty(jumpTarget))
        {
            GameLog.ScriptingError(
                "NewTextAndJumpDialog: text (first argument) or jump target (second argument) cannot be null or empty");
            return null;
        }

        if (name == null)
            sequence = new DialogSequence(Guid.NewGuid().ToString());
        else
            sequence = new DialogSequence(name);
        var dialog = new SimpleDialog(simpleDialog);

        if (!string.IsNullOrEmpty(callback))
            dialog.CallbackExpression = callback;

        sequence.AddDialog(dialog);
        sequence.AddDialog(new JumpDialog(jumpTarget));
        return new HybrasylDialogSequence(sequence);
    }

    /// <summary>
    ///     Another convenience function to generate an "end" sequence where the user must hit close (e.g. a dialog end).
    ///     This is useful to make a jumpable end to a previous dialog option.
    /// </summary>
    /// <param name="simpleDialog">The text of the simple dialog.</param>
    /// <param name="callback">An optional Lua callback expression that will be attached to the simple dialog.</param>
    /// <param name="name">An optional name to give the dialog sequence.</param>
    /// <returns>The constructed dialog sequence</returns>
    public HybrasylDialogSequence NewEndSequence(string simpleDialog, string callback = "", string name = null)
    {
        DialogSequence sequence;

        if (string.IsNullOrEmpty(simpleDialog))
        {
            GameLog.ScriptingError("NewEndSequence: Dialog text (first argument) cannot be null or empty");
            return null;
        }

        if (name == null)
            sequence = new DialogSequence(Guid.NewGuid().ToString());
        else
            sequence = new DialogSequence(name);

        var dialog = new SimpleDialog(simpleDialog);

        if (!string.IsNullOrEmpty(callback))
            dialog.CallbackExpression = callback;

        sequence.AddDialog(dialog);
        return new HybrasylDialogSequence(sequence);
    }

    /// <summary>
    ///     Create a new text dialog (a dialog that asks a player a question; the player can type in a response).
    /// </summary>
    /// <param name="displayText">The text to be displayed in the dialog</param>
    /// <param name="topCaption">The top caption of the text box input</param>
    /// <param name="bottomCaption">The bottom caption of the text box input</param>
    /// <param name="inputLength">
    ///     The maximum length (up to 254 characters) of the text that can be typed into the dialog by
    ///     the player
    /// </param>
    /// <param name="callback">The callback function or lua expression that will fire when this dialog is shown to a player.</param>
    /// <param name="handler">
    ///     The function or lua expression that will handle the response once the player hits enter / hits
    ///     next.
    /// </param>
    /// <returns>The constructed dialog</returns>
    public HybrasylDialog NewTextDialog(string displayText, string topCaption, string bottomCaption,
        int inputLength = 254, string callback = "", string handler = "")
    {
        if (string.IsNullOrEmpty(displayText))
        {
            GameLog.Error("NewTextDialog: display text (first argument) was null");
            return null;
        }

        var dialog = new TextDialog(displayText, topCaption, bottomCaption, inputLength);
        dialog.SetInputHandler(handler);
        dialog.SetCallbackHandler(callback);
        return new HybrasylDialog(dialog);
    }

    /// <summary>
    ///     Create a new options dialog (a dialog that displays clickable options to the player).
    /// </summary>
    /// <param name="displayText">The text to be displayed in the dialog</param>
    /// <param name="dialogOptions">A collection of dialog options (eg HybrasylDialogOptions) associated with this dialog</param>
    /// <param name="callback">A callback function or expression that will fire when this dialog is shown to a player</param>
    /// <param name="handler">
    ///     A callback function or expression that will handle the response once a player selects (clicks) an
    ///     option
    /// </param>
    /// <returns>The constructed dialog</returns>
    public HybrasylDialog NewOptionsDialog(string displayText, HybrasylDialogOptions dialogOptions,
        string callback = "", string handler = "")
    {
        if (string.IsNullOrEmpty(displayText))
        {
            GameLog.ScriptingError("NewOptionsDialog: display text (first argument) cannot be null or empty");
            return null;
        }

        if (dialogOptions is null || dialogOptions.Options.Count == 0)
        {
            GameLog.ScriptingError(
                "NewOptionsDialog: dialogOptions (second or greater argument(s)) null, or had no options");
            return null;
        }

        var dialog = new OptionsDialog(displayText);
        foreach (DictionaryEntry entry in dialogOptions.Options)
            if (entry.Value is string)
            // Callback
            {
                dialog.AddDialogOption(entry.Key as string, entry.Value as string);
            }
            else if (entry.Value is HybrasylDialog)
            {
                var hd = entry.Value as HybrasylDialog;
                if (hd.DialogType == typeof(JumpDialog))
                    // Dialog jump
                    dialog.AddDialogOption(entry.Key as string, hd.Dialog as JumpDialog);
                else
                    GameLog.ScriptingError(
                        "NewOptionsDialog: one or more passed option(s) uses type {type} - only jump dialogs are allowed currently",
                        entry.Value.GetType().Name);
            }
            else if (entry.Value is null)
            // This is JUST an option, with no callback or jump dialog. The dialog handler will process the option itself.
            {
                dialog.AddDialogOption(entry.Key as string);
            }
            else if (entry.Value is HybrasylDialogSequence)
            {
                var hds = entry.Value as HybrasylDialogSequence;
                dialog.AddDialogOption(entry.Key as string, hds.Sequence);
            }
            else
            {
                GameLog.ScriptingError(
                    "NewOptionsDialog: one or more passed option(s) was an unknown type {type} - this will not work",
                    entry.Value.GetType().Name);
            }

        if (dialog.OptionCount == 0)
            GameLog.ScriptingError(
                "NewOptionsDialog: no options were passed or created. This dialog WILL NOT render. DisplayText follows: {displayText}",
                displayText);
        dialog.SetInputHandler(handler);
        dialog.SetCallbackHandler(callback);
        return new HybrasylDialog(dialog);
    }

    /// <summary>
    ///     Create a function dialog, which is an "invisible" dialog that will execute a Lua expression when shown to the
    ///     player. The dialog function will be run,
    ///     and then the next dialog in the sequence will be shown to the player.
    /// </summary>
    /// <param name="luaExpr">The lua expression to run when the FunctionDialog is evaluated</param>
    /// <returns>The constructed dialog</returns>
    public HybrasylDialog NewFunctionDialog(string luaExpr)
    {
        if (string.IsNullOrEmpty(luaExpr))
            GameLog.ScriptingError("NewFunctionDialog: lua expression (first argument) cannot be null or empty");
        return new HybrasylDialog(new FunctionDialog(luaExpr));
    }

    /// <summary>
    ///     Create a jump dialog, which is an "invisible" dialog that is used to start a new sequence from a subdialog. Can be
    ///     used to jump between different NPC dialogue branches.
    /// </summary>
    /// <param name="targetSequence">The name of the sequence that will start when this JumpDialog is "shown" to the player.</param>
    /// <param name="callbackExpression">A lua expression that will run when this dialog is shown to the player.</param>
    /// <returns>The constructed dialog</returns>
    public HybrasylDialog NewJumpDialog(string targetSequence, string callbackExpression = null)
    {
        if (string.IsNullOrEmpty(targetSequence))
        {
            GameLog.ScriptingError("NewJumpDialog: target sequence (first argument) cannot be null or empty");
            return null;
        }

        var dialog = new JumpDialog(targetSequence);
        if (!string.IsNullOrEmpty(callbackExpression))
            dialog.SetCallbackHandler(callbackExpression);
        return new HybrasylDialog(dialog);
    }

    /// <summary>
    ///     Register a dialog sequence as a "global" sequence, meaning any object in the game can reference and use it.
    /// </summary>
    /// <param name="globalSequence">The dialog sequence to be registered as a global seqeunce.</param>
    public void RegisterGlobalSequence(HybrasylDialogSequence globalSequence)
    {
        if (globalSequence is null || globalSequence.Sequence.Dialogs.Count == 0)
            GameLog.ScriptingError(
                "RegisterGlobalSequence: sequence (first argument) was null, or the sequence contained no dialogs");
        Game.World.RegisterGlobalSequence(globalSequence.Sequence);
    }

    public void SpawnMonster(ushort mapId, byte x, byte y, string name, string behaviorSet, int level,
        string displayName = null)
    {
        if (!Game.World.WorldData.TryGetValue(name, out Creature creature)) return;
        if (!Game.World.WorldData.TryGetValue(behaviorSet, out CreatureBehaviorSet cbs)) return;
        if (!Game.World.WorldState.TryGetValue(mapId, out MapObject map)) return;

        var spawn = new Monster(creature, SpawnFlags.Active, (byte)level, null, cbs);

        spawn.X = x;
        spawn.Y = y;
        spawn.Name = displayName ?? name;

        map.InsertCreature(spawn);
    }
}