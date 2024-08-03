﻿// This file is part of Project Hybrasyl.
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hybrasyl.Objects;
using Hybrasyl.Subsystems.Formulas;
using Hybrasyl.Subsystems.Loot;
using Hybrasyl.Xml.Objects;
using Creature = Hybrasyl.Xml.Objects.Creature;
using MessageType = Hybrasyl.Internals.Enums.MessageType;

namespace Hybrasyl.Subsystems.Messaging.ChatCommands;

[WhisperTarget("#")]
public static class EvalCommand
{
    private static readonly Dictionary<Regex, EvalSubcommand> CommandRegexes = new();

    static EvalCommand()
    {
        foreach (var method in typeof(EvalCommand).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            var attr = method.GetCustomAttribute<RegexTrigger>();
            if (attr == null) continue;
            var regex = new Regex(attr.Trigger);
            var cmd = new EvalSubcommand
            {
                Delegate = (Func<User, Match, CommandResult>) Delegate.CreateDelegate(
                    typeof(Func<User, Match, CommandResult>), method)
            };
            var attr2 = method.GetCustomAttribute<UsageText>();
            if (attr2 != null)
                cmd.UsageText = attr2.Text;
            CommandRegexes.Add(regex, cmd);
        }
    }

    private static string UsageTexts => string.Join("\n", CommandRegexes.Values.Select(selector: x => x.UsageText));

    public static CommandResult Success(string response, MessageType type = MessageType.System) =>
        new() { Success = true, Response = response, MessageType = type };

    public static CommandResult Fail(string response, MessageType type = MessageType.System) =>
        new() { Success = false, Response = response, MessageType = type };

    public static void Evaluate(string input, User user)
    {
        foreach (var (key, value) in CommandRegexes)
        {
            var matches = key.Match(input);
            if (!matches.Success) continue;
            var result = value.Delegate(user, matches);
            if (result.ParseError)
            {
                user.DisplayIncomingWhisper("#", value.UsageText);
                return;
            }

            if (!result.Success)
            {
                user.DisplayIncomingWhisper("#", $"ERR: {result.Response}");
                return;
            }

            user.SendMessage(result.Response, result.MessageType);
            return;
        }

        user.SendMessage("Usage:\n" + string.Join(",", UsageTexts), MessageType.SlateScrollbar);
    }

    [RegexTrigger(@"evalloot ""(?<spawngroup>.+)"" ""(?<spawn>.+)"" (?<numevals>\d+)")]
    [UsageText("evalloot <spawngroup name> <spawn name> <number of evals>")]
    public static CommandResult EvalLoot(User user, Match match)
    {
        if (!Game.World.WorldData.TryGetValue(match.Groups["spawngroup"].Value, out SpawnGroup group))
            return Fail("spawngroup not found");
        if (!int.TryParse(match.Groups["numevals"].Value, out var numEvals))
            return Fail("couldn't parse number of evals");

        var spawn = group.Spawns.FirstOrDefault(predicate: x => x.Name == match.Groups["spawn"].Value);

        if (spawn == null)
            return Fail($"Group {group.Name} was found, but not spawn {match.Groups["spawn"].Value}");

        if (!Game.World.WorldData.TryGetValue(spawn.Name, out Creature creature))
            return Fail($"Inexplicably, spawngroup and spawn exist but not the creature {spawn.Name}");

        var loot = new Loot.Loot(0, 0);
        for (var x = 0; x <= numEvals; x++)
        {
            loot += LootBox.CalculateLoot(spawn.Loot);
            loot += LootBox.CalculateLoot(creature.Loot);
            loot += LootBox.CalculateLoot(group.Loot);
        }

        return Success($"Eval for: {group.Name} - {spawn.Name}, {numEvals} rolls\n{loot}", MessageType.SlateScrollbar);
    }

    [RegexTrigger(@"evallootset ""(?<target>.+)"" (?<numrolls>\d+)")]
    [UsageText("evallootset <lootset name> <number of rolls>")]
    public static CommandResult EvalLootset(User user, Match match)
    {
        if (!int.TryParse(match.Groups["numrolls"].Value, out var numEvals))
            return Fail("couldn't parse number of rolls");

        if (!Game.World.WorldData.TryGetValueByIndex(match.Groups["target"].Value, out LootSet set))
            return Fail("lootset not found, or bad number of rolls");

        var loot = LootBox.CalculateLoot(set, numEvals, 1);
        return Success($"Eval for: {set.Name}, {numEvals} rolls\n{loot}", MessageType.SlateScrollbar);
    }

    [RegexTrigger(@"eval ""(?<castable>.+)"" (?<target_id>\d+) (?<numevals>\d+)")]
    [UsageText("eval <castable> <target id> <number of evals>")]
    public static CommandResult EvalDamage(User user, Match match)
    {
        if (!int.TryParse(match.Groups["numevals"].Value, out var numEvals))
            return Fail("couldn't parse number of rolls");
        if (!uint.TryParse(match.Groups["target_id"].Value, out var target_id))
            return Fail("couldn't parse number of rolls");

        if (!Game.World.WorldData.TryGetValueByIndex(match.Groups["castable"].Value, out Castable castable))
            return Fail("Sorry, I couldn't find that castable.");
        if (!Game.World.Objects.TryGetValue(target_id, out var wobj))
            return Fail($"Sorry, I couldn't find object {match.Groups[1].Value}");
        if (wobj is not Objects.Creature creatureObj)
            return Fail("Sorry, that isn't a creature.");

        var damages = new List<DamageOutput>();
        for (var x = 0; x < Convert.ToUInt32(match.Groups["numevals"].Value); x++)
        {
            var output = NumberCruncher.CalculateDamage(castable, creatureObj, user);
            damages.Add(output);
        }

        // Only use "slate" if more than one calculation
        if (damages.Count == 1)
            return Success($"Result: {damages[0].Amount} ({damages[0].Element}, {damages[0].Type})");

        var ret = string.Empty;
        damages.ForEach(action: x => ret += $"{x.Amount} ({x.Element}, {x.Type})\n");
        var avg = damages.Select(selector: x => x.Amount).Average();
        return Success($"Runs: {damages.Count}    Average: {avg}\n\n{ret}", MessageType.SlateScrollbar);
    }
}