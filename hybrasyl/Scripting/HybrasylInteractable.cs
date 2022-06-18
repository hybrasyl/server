using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hybrasyl.Dialogs;
using Hybrasyl.Objects;
using MoonSharp.Interpreter;

namespace Hybrasyl.Scripting
{
    /// <summary>
    /// A scriptable class that can be used to evaluate OnLoad requests to create dialog sequences, which can then
    /// be evaluated later (used specifically for items)
    /// </summary>
    [MoonSharpUserData]
    public class HybrasylInteractable
    {
        public List<DialogSequence> Sequences = new();
        public Dictionary<string, DialogSequence> Index = new();

        public void RegisterDialogSequence(HybrasylDialogSequence wrapped)
        {
            wrapped.Sequence.Id = (uint) (Sequences.Count + 1);
            Sequences.Add(wrapped.Sequence);
            Index.Add(wrapped.Sequence.Name, wrapped.Sequence);
        }
    }
}
