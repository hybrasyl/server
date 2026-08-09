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

using Hybrasyl.Interfaces;
using Hybrasyl.Internals.Logging;
using Hybrasyl.Networking;
using Hybrasyl.Subsystems.Dialogs;
using Hybrasyl.Subsystems.Messaging;
using Hybrasyl.Subsystems.Mundanes;
using Hybrasyl.Subsystems.Scripting;
using Hybrasyl.Xml.Objects;
using NpcOptionResponsePacket = DALib.Networking.Packets.Client.NpcOptionResponsePacket;
using NpcTextResponsePacket = DALib.Networking.Packets.Client.NpcTextResponsePacket;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Hybrasyl.Objects;

public class MerchantInventoryItem(Item item, uint onHand, uint restockAmount, int restockInterval, DateTime lastRestock)
{
    public Item Item { get; } = item;
    public uint OnHand { get; set; } = onHand;
    public uint RestockAmount { get; } = restockAmount;
    public int RestockInterval { get; } = restockInterval;
    public DateTime LastRestock { get; set; } = lastRestock;
}

public sealed class Merchant : Creature, IPursuitable, IEphemeral, ISpawnable
{
    // TODO: move these to new base class (eg Creature->NewClass->Merchant|Reactor etc)
    private readonly object inventoryLock = new();

    public bool Ready;
    public Npc Template;
    public MerchantJob Jobs { get; set; }
    private MerchantController Controller { get; set; }
    public List<MerchantInventoryItem> MerchantInventory { get; set; } = new();

    // TODO: create "computer controllable object" base class and put this there instead
    public Dictionary<string, dynamic> EphemeralStore { get; set; } = new();
    public object StoreLock { get; } = new();
    public List<DialogSequence> Pursuits { get; set; } = new();
    public Dictionary<string, string> Strings { get; set; } = new();
    public Dictionary<string, string> Responses { get; set; } = new();
    public List<DialogSequence> DialogSequences { get; set; } = new();
    public Dictionary<string, DialogSequence> SequenceIndex { get; set; } = new();
    public string DisplayName { get; set; } = string.Empty;

    public Merchant(Npc npc)
    {
        Template = npc;
        Sprite = npc.Appearance.Sprite;
        Portrait = npc.Appearance.Portrait;
        AllowDead = npc.AllowDead;
        Controller = new MerchantController(this);

        foreach (var str in npc.Strings) Strings[str.Key] = str.Value;

        foreach (var resp in npc.Responses)
        {
            var key = resp.Call.ToLower().TrimEnd().TrimStart();
            Responses[key] = resp.Value;
        }

        if (npc.Roles != null)
        {
            if (npc.Roles.Post != null) Jobs ^= MerchantJob.Post;

            if (npc.Roles.Bank != null) Jobs ^= MerchantJob.Bank;

            if (npc.Roles.Repair != null) Jobs ^= MerchantJob.Repair;

            if (npc.Roles.Train != null)
            {
                if (npc.Roles.Train.Any(predicate: x => x.Type == "Skill")) Jobs ^= MerchantJob.Skills;
                if (npc.Roles.Train.Any(predicate: x => x.Type == "Spell")) Jobs ^= MerchantJob.Spells;
            }

            if (npc.Roles.Vend != null) Jobs ^= MerchantJob.Vend;
        }

        Ready = false;
    }

    public override void ShowTo(IVisible obj)
    {
        if (obj is not User user) return;
        user.Enqueue(new DALib.Networking.Packets.Server.DrawObjectsPacket
        {
            Objects =
            [
                new DALib.Networking.Packets.Server.CreatureWorldObject
                {
                    X = X,
                    Y = Y,
                    Id = Id,
                    Sprite = (ushort)(Sprite + 0x4000),
                    Direction = (byte)Direction,
                    Type = DALib.Networking.Packets.Server.CreatureWorldObject.TypeNamed,
                    Name = string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName
                }
            ]
        });
    }


    // TODO: remove this when base(<interface name>) is actually added to the language. .NET 7 maybe
    public string GetLocalString(string key) => ((IResponseCapable) this).DefaultGetLocalString(key);

    public string GetLocalString(string key, params (string Token, string Value)[] replacements) =>
        ((IResponseCapable) this).DefaultGetLocalString(key, replacements);

    public uint GetOnHand(string itemName)
    {
        lock (inventoryLock)
        {
            // An unstocked item reads as 0 on hand.
            var item = MerchantInventory.FirstOrDefault(predicate: x => x.Item.Name == itemName);
            return item?.OnHand ?? 0;
        }
    }

    public void ReduceInventory(string itemName, uint quantity)
    {
        lock (inventoryLock)
        {
            // Reducing an unstocked item is intentionally a no-op.
            var item = MerchantInventory.FirstOrDefault(predicate: x => x.Item.Name == itemName);
            if (item != null) item.OnHand -= quantity;
        }
    }

    public void RestockInventory()
    {
        lock (inventoryLock)
        {
            if (MerchantInventory == null) return;
            foreach (var inventoryItem in MerchantInventory.Where(predicate: inventoryItem =>
                         inventoryItem.LastRestock.AddMinutes(inventoryItem.RestockInterval) < DateTime.Now))
            {
                inventoryItem.OnHand = inventoryItem.RestockAmount;
                inventoryItem.LastRestock = DateTime.Now;
            }
        }
    }

    public List<MerchantInventoryItem> GetOnHandInventory()
    {
        var ret = new List<MerchantInventoryItem>();
        lock (inventoryLock)
        {
            if (MerchantInventory == null) return ret;
            ret.AddRange(MerchantInventory);
        }

        return ret;
    }

    // Currently, NPCs can not be healed or damaged in any way whatsoever
    public override void Heal(double heal, Creature? source = null, Castable? castable = null) { }

    public override void Damage(double damage, ElementType element = ElementType.None,
        DamageType damageType = DamageType.Direct, DamageFlags damageFlags = DamageFlags.None, Creature? attacker = null,
        Castable? castable = null, bool onDeath = true) { }

    public void OnSpawn()
    {
        if (Template.Roles != null && Template.Roles.Vend != null)
        {
            MerchantInventory = new List<MerchantInventoryItem>();

            lock (inventoryLock)
            {
                foreach (var item in Template.Roles.Vend.Items)
                    if (Game.World.WorldData.TryGetValueByIndex(item.Name, out Item worldItem))
                        MerchantInventory.Add(new MerchantInventoryItem(worldItem, (uint) item.Quantity,
                            (uint) item.Quantity, item.Restock, DateTime.Now));
                    else
                        GameLog.Warning("NPC inventory: {name}: {item} not found", Name, item.Name);
            }
        }

        // Do we have a script? If so, get it and run OnSpawn.
        if (World.ScriptProcessor.TryGetScript(Name, out Script? script) || World.ScriptProcessor.TryGetScript(DisplayName, out script))
        {
            DialogSequences.Clear();
            Script = script;
            // Clear existing pursuits, in case the OnSpawn crashes / has a bug
            (this as IPursuitable).ResetPursuits();
            var ret = Script.ExecuteFunction("OnSpawn", ScriptEnvironment.Create(("origin", this)));
            Ready = ret.Result == ScriptResult.Success;
            LastExecutionResult = ret;
            World.ScriptProcessor.RegisterScriptAttachment(script, this);
        }
        else
        {
            Ready = true;
        }
    }

    public override void OnHear(SpokenEvent e)
    {
        if (e.Speaker == this)
            return;

        if (!Ready)
            OnSpawn();

        // Try to evaluate the text as a built-in command
        if (Controller.Evaluate(e))
            return;

        // Call/response?
        if (Responses.TryGetValue(e.SanitizedMessage, out var response))
        {
            // TODO: improve
            Say(response.Replace("$NAME", Name));
            return;
        }

        var resp = World.GetLocalResponse(e.SanitizedMessage);
        if (resp != null)
        {
            Say(resp.Replace("$NAME", Name));
            return;
        }

        // Pass onto a script
        base.OnHear(e);
    }

    public override string Status()
    {
        string ret;
        if (LastExecutionResult == null)
        {
            ret = $"{Name}: script has never executed";
        }
        else
        {
            ret =
                $"NPC {Name}, script {Script!.FileName}\nLast Execution: {LastExecutionResult.Result} at {LastExecutionResult.ExecutionTime}";
            ret = $"{ret}\nExpression: {LastExecutionResult.ExecutedExpression}";
            if (!string.IsNullOrEmpty(LastExecutionResult.Location))
                ret = $"{ret}\nLocation: {LastExecutionResult.Location}";
            if (LastExecutionResult.Error.ErrorType != ScriptErrorType.None)
                ret = $"{ret}\nLast Error: {LastExecutionResult.Error}";
        }

        return ret;
    }

    public override void OnClick(User invoker)
    {
        if (!Ready)
            OnSpawn();

        if (Script != null && Script.HasFunction("OnClick"))
            Script.ExecuteFunction("OnClick", ScriptEnvironment.CreateWithTargetAndSource(invoker, invoker));
        else
        {
                        (this as IPursuitable).DisplayPursuits(invoker);

        }
    }

    public override void AoiEntry(VisibleObject obj)
    {
        base.AoiEntry(obj);
        if (Script != null) Script.ExecuteFunction("OnEntry", ScriptEnvironment.CreateWithTargetAndSource(obj, obj));
    }

    public override void AoiDeparture(VisibleObject obj)
    {
        base.AoiDeparture(obj);
        if (Script != null) Script.ExecuteFunction("OnLeave", ScriptEnvironment.CreateWithTargetAndSource(obj, obj));
    }
}

[Flags]
public enum MerchantJob
{
    None = 0x00,
    Vend = 0x01,
    Bank = 0x02,
    Skills = 0x04,
    Spells = 0x08,
    Repair = 0x10,
    Post = 0x20
}

public enum MerchantMenuItem : ushort
{
    MainMenu = 0xFF00,

    BuyItemMenu = 0xFF01,
    SellItemMenu = 0xFF02,

    WithdrawItemMenu = 0xFF03,
    WithdrawGoldMenu = 0xFF04,
    DepositItemMenu = 0xFF05,
    DepositGoldMenu = 0xFF06,

    LearnSkillMenu = 0xFF07,
    LearnSpellMenu = 0xFF08,
    ForgetSkillMenu = 0xFF09,
    ForgetSpellMenu = 0xFF0A,

    RepairItemMenu = 0xFF0B,
    RepairAllItems = 0xFF0C,

    SendParcelMenu = 0xFF0D,
    SendLetterMenu = 0xFF0E,
    ReceiveParcel = 0xFF0F,

    BuyItem = 0xFF10,
    BuyItemQuantity = 0xFF11,
    BuyItemAccept = 0xFF12,
    SellItem = 0xFF13,
    SellItemQuantity = 0xFF14,
    SellItemConfirm = 0xFF15,
    SellItemAccept = 0xFF16,

    WithdrawItem = 0xFF20,
    WithdrawItemQuantity = 0xFF21,
    DepositItem = 0xFF22,
    DepositItemQuantity = 0xFF23,
    WithdrawGoldQuantity = 0xFF24,
    DepositGoldQuantity = 0xFF25,

    LearnSkill = 0xFF30,
    LearnSkillAccept = 0xFF31,
    LearnSpell = 0xFF32,
    LearnSpellAccept = 0xFF33,
    ForgetSkill = 0xFF34,
    ForgetSkillAccept = 0xFF35,
    ForgetSpell = 0xFF36,
    ForgetSpellAccept = 0xFF37,
    LearnSkillAgree = 0xFF38,
    LearnSkillDisagree = 0xFF39,
    LearnSpellAgree = 0xFF3A,
    LearnSpellDisagree = 0xFF3B,


    RepairItem = 0xFF40,
    RepairItemAccept = 0xFF41,
    RepairAllItemsAccept = 0xFF43,

    SendParcel = 0xFF50,
    SendParcelRecipient = 0xFF51,
    SendParcelAccept = 0xFF52,
    SendParcelSuccess = 0xFF53,
    SendParcelFailure = 0xFF54,
    SendParcelQuantity = 0xFF55
}

public enum MerchantDialogType : byte
{
    Options = 0,
    OptionsWithArgument = 1,
    Input = 2,
    InputWithArgument = 3,
    MerchantShopItems = 4,
    UserInventoryItems = 5,
    MerchantSpells = 6,
    MerchantSkills = 7,
    UserSpellBook = 8,
    UserSkillBook = 9
}

public enum MerchantDialogObjectType : byte
{
    Merchant = 1
}

public struct MerchantOptions
{
    public List<MerchantDialogOption> Options;
}

public struct MerchantDialogOption
{
    public string Text;
    public ushort Id;
}

public struct MerchantInput
{
    public ushort Id;
}

public struct MerchantShopItems
{
    public ushort Id;
    public List<MerchantShopItem> Items;
}

public struct MerchantShopItem
{
    public ushort Tile;
    public byte Color;
    public uint Price;
    public string Name;
    public string Description;
}

public struct UserInventoryItems
{
    public ushort Id;
    public List<byte> InventorySlots;
}

public struct UserSkillBook
{
    public ushort Id;
}

public struct UserSpellBook
{
    public ushort Id;
}

public struct MerchantSpells
{
    public ushort Id;
    public List<MerchantSpell> Spells;
}

public struct MerchantSpell
{
    public byte IconType;
    public byte Icon;
    public byte Color;
    public string Name;
}

public struct MerchantSkills
{
    public ushort Id;
    public List<MerchantSkill> Skills;
}

public struct MerchantSkill
{
    public byte IconType;
    public byte Icon;
    public byte Color;
    public string Name;
}

/// <summary>
///     Which C&#8594;S 0x39 response form a merchant menu item's reply carries. The 0x39 tail is not
///     self-describing: its shape depends on the menu the server last displayed, not on any byte in
///     the packet, so DALib cannot dispatch it and each registration must declare its form here.
/// </summary>
/// <remarks>
///     The form letters are the protocol reference's, from
///     <c>docs/protocol/client/0x39-npc-main-menu.md</c> §"Response tail forms" — Ghidra-verified
///     against the retail client's eleven C&#8594;S 0x39 emitters. Form C (name and quantity in one
///     response) is retail-only: Hybrasyl splits buying into an item-list pick and a separate text
///     prompt, so nothing here ever drives it.
/// </remarks>
public enum MerchantResponseForm
{
    /// <summary>Form A — bare select. The prefix carries everything; there is no tail.</summary>
    Select,

    /// <summary>
    ///     Form B — a trailing <c>string8</c>. Typed input from a text prompt, or the name of the
    ///     row picked out of a server-supplied item, skill or spell list.
    /// </summary>
    Text,

    /// <summary>
    ///     Form E — a trailing option byte. The row picked out of a player-owned list, where the
    ///     option <em>is</em> the inventory or book slot.
    /// </summary>
    Option
}

public delegate void MerchantSelectHandlerDelegate(User user, Merchant merchant);

public delegate void MerchantTextHandlerDelegate(User user, Merchant merchant, string text);

public delegate void MerchantOptionHandlerDelegate(User user, Merchant merchant, byte option);

/// <summary>
///     A merchant menu callback together with the job it requires and the 0x39 response form it
///     expects. The constructor overloads pair form with callback shape, so a callback receives an
///     already-parsed value and cannot read past what its form carries.
/// </summary>
/// <remarks>
///     This is the receive-side counterpart of <c>User.MerchantMenu</c>'s overloads, which pair menu
///     type with body shape on the send side. Declaring the form here also makes it readable, which
///     is what lets <c>MerchantResponseForms</c> diff the two sides.
/// </remarks>
public class MerchantMenuHandler
{
    private readonly Action<User, Merchant, InboundPacket> _invoker;

    public MerchantMenuHandler(MerchantJob requiredJob, MerchantSelectHandlerDelegate callback)
    {
        RequiredJob = requiredJob;
        Form = MerchantResponseForm.Select;
        _invoker = (user, merchant, _) => callback(user, merchant);
    }

    public MerchantMenuHandler(MerchantJob requiredJob, MerchantTextHandlerDelegate callback)
    {
        RequiredJob = requiredJob;
        Form = MerchantResponseForm.Text;
        _invoker = (user, merchant, packet) => callback(user, merchant,
            NpcTextResponsePacket.ParseResponse(packet.Body.Span).Text);
    }

    public MerchantMenuHandler(MerchantJob requiredJob, MerchantOptionHandlerDelegate callback)
    {
        RequiredJob = requiredJob;
        Form = MerchantResponseForm.Option;
        _invoker = (user, merchant, packet) => callback(user, merchant,
            NpcOptionResponsePacket.ParseResponse(packet.Body.Span).Option);
    }

    public MerchantJob RequiredJob { get; }
    public MerchantResponseForm Form { get; }

    /// <summary>
    ///     Parse the body as this handler's declared form and invoke the callback. A malformed
    ///     body throws here rather than mid-callback; the receive loop catches it, logs, and drops
    ///     the packet with the connection intact.
    /// </summary>
    public void Invoke(User user, Merchant merchant, InboundPacket packet) => _invoker(user, merchant, packet);
}
