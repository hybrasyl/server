using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hybrasyl.Dialogs;
using Hybrasyl.Scripting;
using Hybrasyl.Xml;

namespace Hybrasyl.Objects;

public interface IWorldObject
{
    public Rectangle Rect { get; }
    public DateTime CreationTime { get; }
    public bool HasMoved { get; set; }
    public byte X { get; set; }
    public byte Y { get; set; }
    public uint Id { get; set; }
    public string Name { get; set; }
    public Guid ServerGuid { get; set; }
    public World World { get; }
    public ushort DialogSprite { get; }
    public string Type { get; }
}

public interface IDynamicInteractable
{
    public List<DialogSequence> DialogSequences { get; }
    public Dictionary<string, DialogSequence> SequenceIndex { get; }
    public Script Script { get; }

}
