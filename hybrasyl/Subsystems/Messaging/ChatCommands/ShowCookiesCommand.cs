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

using Hybrasyl.Internals.Enums;
using Hybrasyl.Objects;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;
// Various admin commands are implemented here.

internal class ShowCookiesCommand : ChatCommand
{
    public new static string Command = "showcookies";
    public new static string ArgumentText = "<string playername>";
    public new static string HelpText = "Show permanent and session cookies set for a specified player";
    public new static bool Privileged = true;

    public new static ChatCommandResult Run(User user, params string[] args)
    {
        if (Game.World.WorldState.TryGetValue(args[0], out User target))
        {
            var cookies = $"User {target.Name} Cookie List\n\n---Permanent Cookies---\n";
            foreach (var cookie in target.GetCookies())
                cookies = $"{cookies}\n{cookie.Key} : {cookie.Value}\n";
            cookies = $"{cookies}\n---Session Cookies---\n";
            foreach (var cookie in target.GetSessionCookies())
                cookies = $"{cookies}\n{cookie.Key} : {cookie.Value}\n";
            return Success($"{cookies}", MessageTypes.SLATE_WITH_SCROLLBAR);
        }

        return Fail($"User {args[0]} not logged in");
    }
}

//class ScriptingCommand : ChatCommand
//{
//    public new static string Command = "scripting";
//    public new static string ArgumentText = "<reload|disable|enable|status> <string scriptname>";
//    public new static string HelpText = "Reload, disable, enable or request status on the specified script.";
//    public new static bool Privileged = true;

//    public new static ChatCommandResult Run(User user, params string[] args)
//    {

//        if (Game.World.ScriptProcessor.TryGetScript(args[1].Trim(), out Script script))
//        {
//            switch (args[0].ToLower())
//            {
//                case "reload":
//                {
//                    script.Disabled = true;
//                    if (script.Run().Result != ScriptResult.Success)
//                        return Fail($"Script {script.Name}: load/parse error, check scripting log.");
//                    script.Disabled = false;
//                    return Success($"Script {script.Name}: reloaded.");

//                }
//                case "enable":
//                    script.Disabled = false;
//                    return Success($"Script {script.Name}: enabled.");
//                case "disable":
//                    script.Disabled = true;
//                    return Success($"Script {script.Name}: disabled.");
//                case "status":
//                {
//                    var scriptStatus = string.Format("{0}:", script.Name);
//                    string errorSummary = "--- Error Summary ---\n";

//                    errorSummary = script.LastExecutionResult.Result == ScriptResult.Success ? 
//                        $"{errorSummary} no errors": $"{errorSummary} result {script.LastExecutionResult.Result}, {script.LastExecutionResult.Error}";

//                    // Report to the end user
//                    return Success($"{scriptStatus}\n\n{errorSummary}", MessageTypes.SLATE_WITH_SCROLLBAR);
//                }
//            }
//        }
//        return Fail($"Script {args[1].Trim()}: not found.");
//    }
//}

//internal class ReloadXml : ChatCommand
//{
//    public new static string Command = "reloadxml";
//    public new static string ArgumentText = "<string> type <string> filename";

//    public new static string HelpText =
//        "Reloads a specified xml file into world data, i.e. \"castable\" \"all_psk_assail\" (Valid arguments are:\ncastable npc item element lootset nation map itemvariant spawngroup status worldmap localization";

//    public new static bool Privileged = true;

//    public new static ChatCommandResult Run(User user, params string[] args)
//    {
//        if (args.Length < 2) return Fail("Wrong number of arguments supplied.");

//        switch (args[0].ToLower())
//        {
//            case "castable":
//            {
//                //Game.World.Reload(IXmlReloadable);
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedCastable = Castable.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedCastable.Id, out Castable castable))
//                {
//                    Game.World.WorldState.Remove<Castable>(castable.Id);
//                    Game.World.WorldState.SetWithIndex(reloadedCastable.Id, reloadedCastable, reloadedCastable.Name);
//                    foreach (var activeuser in Game.World.ActiveUsers)
//                        if (reloadedCastable.Book == Xml.Objects.Book.PrimarySkill ||
//                            reloadedCastable.Book == Xml.Objects.Book.SecondarySkill ||
//                            reloadedCastable.Book == Xml.Objects.Book.UtilitySkill)
//                        {
//                            if (activeuser.SkillBook.Contains(reloadedCastable.Id))
//                                activeuser.SkillBook[activeuser.SkillBook.SlotOf(reloadedCastable.Id)].Castable =
//                                    reloadedCastable;
//                        }
//                        else
//                        {
//                            if (activeuser.SpellBook.Contains(reloadedCastable.Id))
//                                activeuser.SpellBook[activeuser.SpellBook.SlotOf(reloadedCastable.Id)].Castable =
//                                    reloadedCastable;
//                        }

//                    return Success($"Castable {reloadedCastable.Name} set to world data");
//                }

//                return Fail($"{args[0]} {args[1]} was not found");
//            }
//            case "npc":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedNpc = Npc.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedNpc.Name, out Npc npc))
//                {
//                    Game.World.WorldState.Remove<Npc>(npc.Name);
//                    Game.World.WorldState.Set(reloadedNpc.Name, reloadedNpc);
//                    return Success($"Npc {reloadedNpc.Name} set to world data. Reload NPC to activate.");
//                }

//                return Fail($"{args[0]} {args[1]} was not found");
//            }
//            case "lootset":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedLootSet = LootSet.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedLootSet.Id, out LootSet lootSet))
//                {
//                    Game.World.WorldState.Remove<LootSet>(lootSet.Id);
//                    Game.World.WorldState.SetWithIndex(reloadedLootSet.Id, reloadedLootSet, reloadedLootSet.Name);
//                    return Success($"LootSet {reloadedLootSet.Name} set to world data");
//                }

//                return Fail($"{args[0]} {args[1]} was not found");
//            }
//            case "nation":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedNation = Nation.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedNation.Name, out Nation nation))
//                {
//                    Game.World.WorldState.Remove<Nation>(nation.Name);
//                    Game.World.WorldState.Set(reloadedNation.Name, reloadedNation);
//                    return Success($"Nation {reloadedNation.Name} set to world data");
//                }

//                return Fail($"{args[0]} {args[1]} was not found");
//            }
//            case "map":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedMap = Xml.Objects.Map.LoadFromFile(reloaded);

//                if (!Game.World.WorldState.TryGetValue(reloadedMap.Id, out Map map))
//                    return Fail($"{args[0]} {args[1]} was not found");

//                var newMap = new Map(reloadedMap, Game.World);
//                Game.World.WorldState.RemoveIndex<Map>(map.Name);
//                Game.World.WorldState.Remove<Map>(map.Id);
//                var mapObjs = map.Objects.ToList();
//                foreach (var obj in mapObjs) 
//                {
//                    map.Remove(obj);
//                    switch (obj)
//                    {
//                        case User usr:
//                            newMap.Insert(usr, usr.X, usr.Y);
//                            break;
//                        case Monster mob:
//                            Game.World.Remove(mob);
//                            break;
//                        case ItemObject itm:
//                            Game.World.Remove(itm);
//                            break;
//                        case Merchant npc:
//                            npc.Map = newMap;
//                            break;
//                    }
//                }
//                Game.World.WorldState.SetWithIndex(newMap.Id, newMap, newMap.Name);

//                return Success($"Map {reloadedMap.Name} set to world data");

//            }
//            case "item":
//            {
//                return Fail("Not yet supported.");
//            }
//            case "itemvariant":
//            {
//                return Fail("Not supported.");
//            }
//            case "spawngroup":
//            {
//                return Fail("Not supported yet");
//            }
//            case "status":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedStatus = Status.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedStatus.Name, out Status status))
//                {
//                    Game.World.WorldState.Remove<Status>(status.Name);
//                    Game.World.WorldState.Set(reloadedStatus.Name, reloadedStatus);
//                    return Success($"Status {reloadedStatus.Name} set to world data");
//                }

//                return Fail($"{args[0]} {args[1]} was not found");
//            }
//            case "worldmap":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedWorldMap = Xml.Objects.WorldMap.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedWorldMap.Name, out Xml.Objects.WorldMap status))
//                {
//                    Game.World.WorldState.Remove<Xml.Objects.WorldMap>(status.Name);
//                    Game.World.WorldState.Set(reloadedWorldMap.Name, reloadedWorldMap);
//                    return Success($"WorldMap {reloadedWorldMap.Name} set to world data");
//                }

//                return Fail($"{args[0]} {args[1]} was not found");
//            }
//            case "element":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedElementTable = ElementTable.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue("ElementTable", out ElementTable table))
//                {
//                    Game.World.WorldState.Remove<ElementTable>("ElementTable");
//                    Game.World.WorldState.Set("ElementTable", reloadedElementTable);
//                    return Success("ElementTable set to world data");
//                }

//                return Fail($"{args[0]} {args[1]} was not found");
//            }
//            case "localization":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                Game.World.Strings = LocalizedStringGroup.LoadFromFile(reloaded);
//                return Success("Localization strings set to World");
//            }
//            default:
//                return Fail("Bad input.");
//        }
//    }
//}

//internal class LoadXml : ChatCommand
//{
//    public new static string Command = "loadxml";
//    public new static string ArgumentText = "<string> type <string> filename";

//    public new static string HelpText =
//        "Loads a specified xml file into world data, i.e. \"castable\" \"wizard_psp_srad\" (Valid arguments are: \n\n castable npc item lootset nation map itemvariant spawngroup status worldmap";

//    public new static bool Privileged = true;

//    public new static ChatCommandResult Run(User user, params string[] args)
//    {
//        if (args.Length < 2) return Fail("Wrong number of arguments supplied.");

//        switch (args[0].ToLower())
//        {
//            case "castable":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedCastable = Castable.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedCastable.Id, out Castable castable))
//                    return Fail($"{args[0]} {args[1]} already exists.");
//                Game.World.WorldState.SetWithIndex(reloadedCastable.Id, reloadedCastable, reloadedCastable.Name);
//                return Success($"Castable {reloadedCastable.Name} set to world data");
//            }
//            case "npc":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedNpc = Npc.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedNpc.Name, out Npc npc))
//                    return Fail($"{args[0]} {args[1]} already exists.");
//                Game.World.WorldState.Set(reloadedNpc.Name, reloadedNpc);
//                return Success($"Npc {reloadedNpc.Name} set to world data.");
//            }
//            case "lootset":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedLootSet = LootSet.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedLootSet.Id, out LootSet lootSet))
//                    return Fail($"{args[0]} {args[1]} already exists.");
//                Game.World.WorldState.SetWithIndex(reloadedLootSet.Id, reloadedLootSet, reloadedLootSet.Name);
//                return Success($"Npc {reloadedLootSet.Name} set to world data.");
//            }
//            case "nation":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedNation = Nation.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedNation.Name, out Nation nation))
//                    return Fail($"{args[0]} {args[1]} already exists.");
//                Game.World.WorldState.Set(reloadedNation.Name, reloadedNation);
//                return Success($"Nation {reloadedNation.Name} set to world data");
//            }
//            case "map":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedMap = Xml.Objects.Map.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedMap.Id, out Map map))
//                    return Fail($"{args[0]} {args[1]} already exists.");
//                var newMap = new Map(reloadedMap, Game.World);
//                Game.World.WorldState.SetWithIndex(newMap.Id, newMap, newMap.Name);
//                return Success($"Map {reloadedMap.Name} set to world data");
//            }
//            case "item":
//            {
//                return Fail("Not yet supported.");
//            }
//            case "itemvariant":
//            {
//                return Fail("Not supported.");
//            }
//            case "spawngroup":
//            {
//                return Fail("Not supported, yet");
//            }
//            case "status":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedStatus = Status.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedStatus.Name, out Status status))
//                    return Fail($"{args[0]} {args[1]} already exists.");
//                Game.World.WorldState.Set(reloadedStatus.Name, reloadedStatus);
//                return Success($"Status {reloadedStatus.Name} set to world data");
//            }
//            case "worldmap":
//            {
//                var reloaded = Game.World.GetXmlFile(args[0], args[1]);
//                var reloadedWorldMap = Xml.Objects.WorldMap.LoadFromFile(reloaded);

//                if (Game.World.WorldState.TryGetValue(reloadedWorldMap.Name, out Xml.Objects.WorldMap status))
//                    return Fail($"{args[0]} {args[1]} already exists.");
//                Game.World.WorldState.Set(reloadedWorldMap.Name, reloadedWorldMap);
//                return Success($"WorldMap {reloadedWorldMap.Name} set to world data");
//            }
//            default:
//                return Fail("Bad input.");
//        }
//    }
//}