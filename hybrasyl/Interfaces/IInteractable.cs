using System.Collections.Generic;
using Hybrasyl.Dialogs;
using Hybrasyl.Scripting;

namespace Hybrasyl.Interfaces;

public interface IInteractable : ISprite
{
    public Script Script { get; }
    public string Name { get; }
    public uint Id { get; }
    public bool AllowDead { get; }

    public List<DialogSequence> DialogSequences { get; set; }
    public Dictionary<string, DialogSequence> SequenceIndex { get; set; }
    public ushort DialogSprite { get; }

    public virtual void RegisterDialogSequence(DialogSequence sequence)
    {
        sequence.Id = (uint) (Constants.DIALOG_SEQUENCE_PURSUITS + DialogSequences.Count);
        //sequence.AssociateSequence(this);
        DialogSequences.Add(sequence);

        if (SequenceIndex.ContainsKey(sequence.Name))
        {
            GameLog.WarningFormat("Dialog sequence {0} is being overwritten", sequence.Name);
            SequenceIndex.Remove(sequence.Name);
        }

        SequenceIndex.Add(sequence.Name, sequence);
    }
}