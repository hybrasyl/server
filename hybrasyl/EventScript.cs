using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hybrasyl
{
    // EventScript is a shorthand for scripting event effects, dialog and more.
    // Example:
    // "* begin deoch && sub start && eff 7 && npcshout Riona "OH NO" && eff 35 && end && installtrigger Trigger1 "hello" start
    // mapeff <effect> - Display effect on all players on current map.
    // eff <x,y> <effect> - Display effect at x,y on current map.
    // eff <Name> <effect> - Display effect centered on player.
    // npcsay <name> <line> - NPC on current map will say line.
    // npcshout <name> <line> - NPC on current map will shout line.
    // pause <int> - Pause execution of eventscript.
    // installtrigger <name> <trigger string> <event> 
    // mapitem <item> [<qty>] - Give item to all players on map
    // mapsys <msg> - Send system message to all players on map
    // end
    // *: Deoch: accepted
    // "* run deoch
    // *: Deoch: running
    // Collapse to multiple lines by usage of &&
    // reff 6 && rsys "Hello && pause 6 && npcsay riona "Sigh"

    class EventScriptParser
    {
        public void AddLine(string line)
        {
            var commands = line.Trim().Split("&&");

        }

        public bool ParseLine(string line)
        {
            return false;
        }
    }

    interface IEventScriptCommand
    {
        string name { get; set; }
    }

    class EventScript
    {
        List<Action> Actions { get; set; }
        DateTime Start { get; set; }
        string Name { get; set; }

    }
}
