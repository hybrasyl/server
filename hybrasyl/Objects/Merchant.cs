/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2020 ERISCO, LLC 
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */
 
using Hybrasyl.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Hybrasyl.Controllers;
using Hybrasyl.Enums;
using Hybrasyl.Interfaces;
using Hybrasyl.Messaging;
using Hybrasyl.Xml;

namespace Hybrasyl.Objects;

public class MerchantInventoryItem
{
        

    public Xml.Item Item { get; private set; }
    public uint OnHand { get; set; }
    public uint RestockAmount { get; private set; }
    public int RestockInterval { get; private set; }
    public DateTime LastRestock { get; set; }

    public MerchantInventoryItem(Xml.Item item, uint onHand, uint restockAmount, int restockInterval, DateTime lastRestock)
    {
        Item = item;
        OnHand = onHand;
        RestockAmount = restockAmount;
        RestockInterval = restockInterval;
        LastRestock = lastRestock;
    }

}


public class Merchant : Creature, IXmlReloadable
{
    private readonly object inventoryLock = new();

    public bool Ready;
    public string Filename { get; set; }
    public Npc Template;
    public MerchantJob Jobs { get; set; }
    private MerchantController Controller { get; set; }
    public List<MerchantInventoryItem> MerchantInventory { get; set; }

    public Merchant(Npc npc)
        : base()
    {
        Template = npc;
        Sprite = npc.Appearance.Sprite;
        Portrait = npc.Appearance.Portrait;
        AllowDead = npc.AllowDead;
        Controller = new MerchantController(this);

        foreach (var str in npc.Strings)
        {
            Strings[str.Key] = str.Value;
        }

        foreach (var resp in npc.Responses)
        {
            var key = resp.Call.ToLower().TrimEnd().TrimStart();
            Responses[key] = resp.Value;
        }

        if (npc.Roles != null)
        {
            if (npc.Roles.Post != null)
            {
                Jobs ^= MerchantJob.Post;
            }

            if (npc.Roles.Bank != null)
            {
                Jobs ^= MerchantJob.Bank;
            }

            if (npc.Roles.Repair != null)
            {
                Jobs ^= MerchantJob.Repair;
            }

            if (npc.Roles.Train != null)
            {
                if (npc.Roles.Train.Any(x => x.Type == "Skill")) Jobs ^= MerchantJob.Skills;
                if (npc.Roles.Train.Any(x => x.Type == "Spell")) Jobs ^= MerchantJob.Spells;
            }

            if (npc.Roles.Vend != null)
            {
                Jobs ^= MerchantJob.Vend;
            }

        }

        Ready = false;
    }

    public uint GetOnHand(string itemName)
    {
        lock(inventoryLock)
        {
            return MerchantInventory.FirstOrDefault(x => x.Item.Name == itemName).OnHand;
        }
    }

    public void ReduceInventory(string itemName, uint quantity)
    {
        lock(inventoryLock)
        {
            MerchantInventory.FirstOrDefault(x => x.Item.Name == itemName).OnHand -= quantity;
        }
    }

    public void RestockInventory()
    {
        lock(inventoryLock)
        {
            if (MerchantInventory == null) return;
            foreach (var inventoryItem in MerchantInventory.Where(inventoryItem => inventoryItem.LastRestock.AddMinutes(inventoryItem.RestockInterval) < DateTime.Now))
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
    public override void Heal(double heal, Creature source = null) { return; }
    public override void Damage(double damage, Xml.ElementType element = Xml.ElementType.None, Xml.DamageType damageType = Xml.DamageType.Direct, Xml.DamageFlags damageFlags = Xml.DamageFlags.None, Creature attacker = null, bool onDeath = true) { return; }

    public void OnSpawn()
    {
        if (Template.Roles != null && Template.Roles.Vend != null)
        {
            MerchantInventory = new List<MerchantInventoryItem>();

            lock (inventoryLock)
            {
                foreach (var item in Template.Roles.Vend.Items)
                {
                    if (Game.World.WorldData.TryGetValueByIndex(item.Name, out Xml.Item worldItem))
                        MerchantInventory.Add(new MerchantInventoryItem(worldItem, (uint) item.Quantity,
                            (uint) item.Quantity, item.Restock, DateTime.Now));
                    else
                        GameLog.Warning("NPC inventory: {name}: {item} not found", Name, item.Name);
                }
            }
        }

        Script script;
        // Do we have a script? If so, get it and run OnSpawn.
        if (World.ScriptProcessor.TryGetScript(Name, out script))
        {
            Script = script;
            Script.AssociateScriptWithObject(this);
            // Clear existing pursuits, in case the OnSpawn crashes / has a bug
            ResetPursuits();
            var ret = Script.ExecuteFunction("OnSpawn", ScriptEnvironment.CreateWithInvoker(this));
            Ready = ret.Result == ScriptResult.Success;
        }
        else
            Ready = true;
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
        if (Responses.TryGetValue(e.SanitizedMessage, out string response) )
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
            ret = $"{Name}: script has never executed";
        else
        {
            ret =
                $"NPC {Name}, script {Script.FileName}\nLast Execution: {LastExecutionResult.Result} at {LastExecutionResult.ExecutionTime}";
            ret = $"{ret}\nExpression: {LastExecutionResult.ExecutedExpression}";
            if (!string.IsNullOrEmpty(LastExecutionResult.Location))
                ret = $"{ret}\nLocation: {LastExecutionResult.Location}";
            if (LastExecutionResult.Error != null)
                ret = $"{ret}\nLast Error: {LastExecutionResult.Error}";
        }

        return ret;
    }

    public override void OnClick(User invoker)
    {
        if (!Ready)
            OnSpawn();

        if (Script != null && Script.HasFunction("OnClick"))
            Script.ExecuteFunction("OnClick", ScriptEnvironment.CreateWithInvoker(invoker));
        else
            DisplayPursuits(invoker);
    }

    public override void AoiEntry(VisibleObject obj)
    {
        base.AoiEntry(obj);
        if (Script != null)
        {
            Script.ExecuteFunction("OnEntry", ScriptEnvironment.CreateWithInvoker(obj));
        }
    }

    public override void AoiDeparture(VisibleObject obj)
    {
        base.AoiDeparture(obj);
        if (Script != null)
        {
            Script.ExecuteFunction("OnLeave", ScriptEnvironment.CreateWithInvoker(obj));
        }
    }

    public override void ShowTo(VisibleObject obj)
    {
        if (obj is User)
        {
            var user = obj as User;
            var npcPacket = new ServerPacket(0x07);
            npcPacket.WriteUInt16(0x01); // Number of mobs in this packet
            npcPacket.WriteUInt16(X);
            npcPacket.WriteUInt16(Y);
            npcPacket.WriteUInt32(Id);
            npcPacket.WriteUInt16((ushort)(Sprite + 0x4000));
            npcPacket.WriteByte(0);
            npcPacket.WriteByte(0);
            npcPacket.WriteByte(0);
            npcPacket.WriteByte(0);
            npcPacket.WriteByte((byte)Direction);
            npcPacket.WriteByte(0);

            npcPacket.WriteByte(2); // Dot color. 0 = monster, 1 = nonsolid monster, 2=NPC
            npcPacket.WriteString8(Name);
            user.Enqueue(npcPacket);
        }
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
    SendParcelQuantity = 0xFF55,
        
        

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
    public byte OptionsCount => Convert.ToByte(Options.Count);
    public List<MerchantDialogOption> Options;
}

public struct MerchantOptionsWithArgument
{
    public byte ArgumentLength => Convert.ToByte(Argument.Length);
    public string Argument;
    public byte OptionsCount => Convert.ToByte(Options.Count);
    public List<MerchantDialogOption> Options;
}

public struct MerchantDialogOption
{
    public byte Length => Convert.ToByte(Text.Length);
    public string Text;
    public ushort Id;
}

public struct MerchantInput
{
    public ushort Id;
}

public struct MerchantInputWithArgument
{
    public byte ArgumentLength => Convert.ToByte(Argument.Length);
    public string Argument;
    public ushort Id;
}

public struct MerchantShopItems
{
    public ushort Id;
    public ushort ItemsCount => Convert.ToUInt16(Items.Count);
    public List<MerchantShopItem> Items;
}

public struct MerchantShopItem
{
    public ushort Tile;
    public byte Color;
    public uint Price;
    public byte NameLength => Convert.ToByte(Name.Length);
    public string Name;
    public byte DescriptionLength => Convert.ToByte(Description.Length);
    public string Description;
}

public struct UserInventoryItems
{
    public ushort Id;
    public byte InventorySlotsCount => Convert.ToByte(InventorySlots.Count);
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
    public ushort SpellsCount => Convert.ToUInt16(Spells.Count());
    public byte IconType;
    public List<MerchantSpell> Spells;
}

public struct MerchantSpell
{
    public byte IconType;
    public byte Icon;
    public byte Color;
    public byte NameLength => Convert.ToByte(Name.Length);
    public string Name;
}

public struct MerchantSkills
{
    public ushort Id;
    public ushort SkillsCount => Convert.ToUInt16(Skills.Count());
    public byte IconType;
    public List<MerchantSkill> Skills;
}

public struct MerchantSkill
{
    public byte IconType;
    public byte Icon;
    public byte Color;
    public byte NameLength => Convert.ToByte(Name.Length);
    public string Name;
}




public delegate void MerchantMenuHandlerDelegate(User user, Merchant merchant, ClientPacket packet);

public class MerchantMenuHandler
{
    public MerchantJob RequiredJob { get; set; }
    public MerchantMenuHandlerDelegate Callback { get; set; }
    public MerchantMenuHandler(MerchantJob requiredJob, MerchantMenuHandlerDelegate callback)
    {
        RequiredJob = requiredJob;
        Callback = callback;
    }
}