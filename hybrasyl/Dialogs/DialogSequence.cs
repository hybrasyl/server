using System.Collections.Generic;
using System.Linq;
using Hybrasyl.Interfaces;
using Hybrasyl.Objects;
using Hybrasyl.Scripting;
using MoonSharp.Interpreter;
using Serilog;

namespace Hybrasyl.Dialogs;
public class DialogSequence
{
    public List<Dialog> Dialogs { get; private set; }
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public uint? Id { get; set; }

    private Scripting.Script _script;
    public string ScriptName;

    private Dictionary<string, string> Tokens { get; set; }

    public Scripting.Script Script
    {
        // This allows a form of lazy evaluation to prevent chicken in egg problems with registering
        // dialogs associated with a running script which is in the process of registering said dialogs
        get
        {
            if (_script == null && !string.IsNullOrEmpty(ScriptName))
            {
                if (Game.World.ScriptProcessor.TryGetScript(ScriptName, out Scripting.Script _script))
                    return _script;
                else
                {
                    GameLog.Error($"DialogSequence {Name}: script associate {ScriptName} is missing");
                    return null;
                }
            }
            if (_script != null)
                return _script;
            return null;
        }
        set { _script = value; }
    }

    public IInteractable Associate { get; private set; }
    public string PreDisplayCallback { get; private set; }
    public string MenuCheckExpression { get; private set; }
    public bool CloseOnEnd { get; set; }

    public ushort Sprite { get; set; }

    public DialogSequence(string sequenceName, bool closeOnEnd = false)
    {
        Name = sequenceName;
        Dialogs = new List<Dialog>();
        Id = null;
        CloseOnEnd = closeOnEnd;
        PreDisplayCallback = string.Empty;
        MenuCheckExpression = string.Empty;
        Sprite = ushort.MinValue;
        ScriptName = string.Empty;
        _script = null;
        DisplayName = string.Empty;
    }

    /// <summary>
    /// Show a dialog sequence to a user.
    /// </summary>
    /// <param name="invoker">The user who will receive the dialog.</param>
    /// <param name="target">A target of the dialog; generally an associate (NPC/reactor tile)</param>
    /// <param name="runCheck">Whether or not to run any pre display checks before displaying the sequence</param>
    public void ShowTo(User invoker, IInteractable target = null, bool runCheck = true)
    {
        // Either we must have an associate already known to us, one must be passed, or we must have a script defined
        if (Associate == null && target == null && Script == null)
        {
            Log.Error("DialogSequence {0} has no known associate or script...?", Name);
            // Need better error handling here
            return;
        }
        if (!string.IsNullOrEmpty(PreDisplayCallback) && runCheck)
        {
            var env = ScriptEnvironment.CreateWithOrigin(invoker);
            env.DialogPath = Name;
            var ret = Script.ExecuteExpression(PreDisplayCallback, env);
            if (ret.Return.Equals(DynValue.True))
                Dialogs.First().ShowTo(invoker, target);
            else
                // Error, generally speaking
                invoker.ClearDialogState();
        }
        else
        {
            Dialogs.First().ShowTo(invoker, target);
        }
    }
    /// <summary>
    /// Associate a dialog with an object in the world.
    /// </summary>
    /// <param name="obj"></param>
    public void AssociateSequence(IInteractable obj)
    {
        Associate = obj;
    }

    public void AddDialog(Dialog dialog)
    {
        dialog.Index = Dialogs.Count();
        dialog.AssociateWithSequence(this);
        Dialogs.Add(dialog);
    }

    public void AddPreDisplayCallback(string check)
    {
        PreDisplayCallback = check;
    }

    public void AddMenuCheckExpression(string check)
    {
        MenuCheckExpression = check;
    }

    /// <summary>
    /// Skip to the specified index in a dialog sequence.
    /// </summary>
    /// <param name="index"></param>
    /// <param name="invoker"></param>
    /// <param name="target"></param>
    public void ShowByIndex(int index, User invoker, IInteractable target = null)
    {
        if (index >= Dialogs.Count)
            return;
        Dialogs[index].ShowTo(invoker, target);
    }
}
