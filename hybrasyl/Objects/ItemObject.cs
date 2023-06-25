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

using Hybrasyl.Dialogs;
using Hybrasyl.Enums;
using Hybrasyl.Interfaces;
using Hybrasyl.Scripting;
using Hybrasyl.Threading;
using Hybrasyl.Xml.Objects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Hybrasyl.Objects;

public class ItemObject : VisibleObject, IInteractable
{
    private Item _template;

    public ItemObject(string id, Guid containingWorld = default, Guid guid = default)
    {
        ServerGuid = containingWorld;
        TemplateId = id;
        _durability = new Lockable<double>(MaximumDurability);
        _count = new Lockable<int>(1);
        Guid = guid != default ? guid : Guid.NewGuid();
    }

    public ItemObject(Item template, Guid containingWorld = default, Guid guid = default)
    {
        Template = template;
        TemplateId = template.Id;
        ServerGuid = containingWorld;
        _count = new Lockable<int>(1);
        _durability = new Lockable<double>(MaximumDurability);
        Guid = guid != default ? guid : Guid.NewGuid();
    }

    // Simple copy constructor for an ItemObject, mostly used when we split a stack and it results
    // in the creation of a new ItemObject.
    public ItemObject(ItemObject previousItemObject)
    {
        _count = new Lockable<int>(previousItemObject.Count);
        _durability = new Lockable<double>(previousItemObject.Durability);
        ServerGuid = previousItemObject.ServerGuid;
        TemplateId = previousItemObject.TemplateId;
        Durability = previousItemObject.Durability;
        Count = previousItemObject.Count;
        Guid = Guid.NewGuid();
    }

    public string TemplateId { get; private set; }

    public StatInfo Stats { get; private set; }

    public Item Template
    {
        get => _template ?? World.WorldData.Get<Item>(TemplateId);
        set => _template = value;
    }

    public bool Usable => Template.Properties.Use != null;
    public Use Use => Template.Properties.Use;

    public ushort EquipSprite => Template.Properties.Appearance.EquipSprite == 0
        ? Template.Properties.Appearance.Sprite
        : Template.Properties.Appearance.EquipSprite;

    public ItemObjectType ItemObjectType
    {
        get
        {
            if ((Template?.Properties?.Equipment?.Slot ?? Xml.Objects.EquipmentSlot.None) != Xml.Objects.EquipmentSlot.None)
                return ItemObjectType.Equipment;
            if (Template.Properties.Flags.HasFlag(ItemFlags.Consumable) || Template.Use != null)
                return ItemObjectType.CanUse;
            return ItemObjectType.CannotUse;
        }
    }

    public WeaponType WeaponType => Template.Properties.Equipment.WeaponType;
    public byte EquipmentSlot => Convert.ToByte(Template.Properties.Equipment.Slot);
    public string SlotName => Enum.GetName(typeof(EquipmentSlot), EquipmentSlot) ?? "None";

    public int Weight => Template.Properties.Physical.Weight > int.MaxValue
        ? int.MaxValue
        : Convert.ToInt32(Template.Properties.Physical.Weight);

    public int MaximumStack => Template.MaximumStack;
    public bool Stackable => Template.Stackable;

    public List<CastModifier> CastModifiers => Template.Properties.CastModifiers;

    public uint MaximumDurability => Template.Properties?.Physical?.Durability > uint.MaxValue
        ? uint.MaxValue
        : Convert.ToUInt32(Template.Properties.Physical.Durability);

    public uint RepairCost
    {
        get
        {
            if (MaximumDurability != 0)
                return Durability == 0 ? Value : (uint)(Durability / MaximumDurability * Value);
            return 0;
        }
    }

    // For future use / expansion re: unidentified items.
    // Should pull from template and only allow false to be set when
    // Identifiable flag is set.
    public bool Identified => true;

    public byte MinLevel => Template.MinLevel;
    public byte MinAbility => Template.MinAbility;
    public byte MaxLevel => Template.MaxLevel;
    public byte MaxAbility => Template.MaxAbility;

    public Class Class => Template.Class;
    public Gender Gender => Template.Gender;

    public byte Color => Convert.ToByte(Template.Properties.Appearance.Color);
    public List<string> Categories => Template.CategoryList;

    public byte BodyStyle => Convert.ToByte(Template.Properties.Appearance.BodyStyle);

    public ElementType Element => Template.Element;

    public float MinLDamage => Template.MinLDamage;
    public float MaxLDamage => Template.MaxLDamage;
    public float MinSDamage => Template.MinSDamage;
    public float MaxSDamage => Template.MaxSDamage;
    public ushort DisplaySprite => Template.Properties.Appearance.DisplaySprite;

    public uint Value => Template.Properties.Physical.Value > uint.MaxValue
        ? uint.MaxValue
        : Convert.ToUInt32(Template.Properties.Physical.Value);

    public bool HideBoots => Template.Properties.Appearance.HideBoots;

    public byte AssailSound => Template.Properties?.Use?.Sound?.Id ?? 1;
    public List<Proc> Procs => Template.Properties.Procs;

    public bool Enchantable => Template.Properties.Flags.HasFlag(ItemFlags.Enchantable);
    public bool Depositable => Template.Properties.Flags.HasFlag(ItemFlags.Depositable);

    public bool Consecratable => Template.Properties.Flags.HasFlag(ItemFlags.Consecratable);

    public bool Tailorable => Template.Properties.Flags.HasFlag(ItemFlags.Tailorable);

    public bool Smithable => Template.Properties.Flags.HasFlag(ItemFlags.Smithable);

    public bool Exchangeable => Template.Properties.Flags.HasFlag(ItemFlags.Exchangeable);

    public bool MasterOnly => Template.Properties.Flags.HasFlag(ItemFlags.MasterOnly);

    public bool Perishable => Template.Properties.Flags.HasFlag(ItemFlags.Perishable);

    public bool UniqueInventory => Template.Properties.Flags.HasFlag(ItemFlags.UniqueInventory);

    public bool UniqueEquipped => Template.Properties.Flags.HasFlag(ItemFlags.UniqueEquipped);

    public bool Consumable => Template.Properties.Flags.HasFlag(ItemFlags.Consumable);

    public bool Undamageable => Template.Properties.Flags.HasFlag(ItemFlags.Undamageable);
    public bool Bound => Template.Properties.Flags.HasFlag(ItemFlags.Bound);

    public bool IsVariant => Template.IsVariant;

    public Item ParentItem => Template.ParentItem;

    public Variant CurrentVariant => Template.CurrentVariant;

    private Lockable<int> _count { get; set; }

    public int Count
    {
        get => _count.Value;
        set => _count.Value = value;
    }

    private Lockable<double> _durability { get; set; }

    public double Durability
    {
        get => _durability.Value;
        set => _durability.Value = value;
    }

    public uint DisplayDurability => Convert.ToUInt32(Math.Round(Durability));

    public new string Name => Template.Name;

    public new ushort Sprite => Template.Properties.Appearance.Sprite;

    public virtual List<DialogSequence> DialogSequences
    {
        get => Game.World.WorldState.Get<HybrasylInteractable>(Template.Id).Sequences;
        set => throw new NotImplementedException();
    }

    public virtual Dictionary<string, DialogSequence> SequenceIndex
    {
        get => Game.World.WorldState.Get<HybrasylInteractable>(Template.Id).Index;
        set => throw new NotImplementedException();
    }

    public virtual Script Script
    {
        get => Game.World.ScriptProcessor.TryGetScript(Use.Script, out var script) ? script : null;
        set => throw new NotImplementedException();
    }

    /// <summary>
    ///     Check to see if a specified user can equip an ItemObject. Returns a boolean indicating whether
    ///     the ItemObject can be equipped and if not, sets the message reference to contain an appropriate
    ///     message to be sent to the user.
    /// </summary>
    /// <param name="userobj">User object to check for meeting this ItemObject's requirements.</param>
    /// <param name="message">A reference that will be used in the case of failure to set an appropriate error message.</param>
    /// <returns></returns>
    public bool CheckRequirements(User userobj, out string message)
    {
        // We check a variety of conditions and return the first failure.

        message = string.Empty;

        // Check gender

        if (Gender != 0 && Gender != userobj.Gender)
        {
            message = World.GetLocalString("item_equip_wrong_gender");
            return false;
        }

        // Check class

        if (userobj.Class != Class && Class != Class.Peasant)
        {
            message = userobj.Class == Class.Peasant
                ? World.GetLocalString("item_equip_peasant")
                : World.GetLocalString("item_equip_wrong_class");
            return false;
        }

        // Check level / AB

        if (userobj.Stats.Level < MinLevel || (MinAbility != 0 && userobj.Stats.Ability < MinAbility))
        {
            message = World.GetLocalString("item_equip_more_insight");
            return false;
        }

        if (userobj.Stats.Level > MaxLevel || userobj.Stats.Ability > MaxAbility)
        {
            message = World.GetLocalString("item_equip_less_insight");
            return false;
        }

        if (userobj.Equipment.Weight + Weight > userobj.MaximumWeight / 2)
        {
            message = World.GetLocalString("item_equip_too_heavy");
            return false;
        }

        // Check if user is equipping a shield while holding a two-handed weapon

        if (EquipmentSlot == (byte)ItemSlots.Shield && userobj.Equipment.Weapon != null &&
            userobj.Equipment.Weapon.WeaponType == WeaponType.TwoHand)
        {
            message = World.GetLocalString("item_equip_shield_2h");
            return false;
        }

        // Check if user is equipping a two-handed weapon while holding a shield

        if (EquipmentSlot == (byte)ItemSlots.Weapon &&
            (WeaponType == WeaponType.TwoHand || WeaponType == WeaponType.Staff) &&
            userobj.Equipment.Shield != null)
        {
            message = World.GetLocalString("item_equip_2h_shield");
            return false;
        }

        // Check unique-equipped
        if (UniqueEquipped && userobj.Equipment.FindById(TemplateId) != null)
        {
            message = World.GetLocalString("item_equip_unique_equipped");
            return false;
        }

        // Check item slot prohibitions

        // This code is intentionally verbose
        foreach (var restriction in Template.Properties.Restrictions?.SlotRestrictions ?? new List<SlotRestriction>())
        {
            var restrictionMessage = World.GetLocalString(restriction.Message == string.Empty
                ? "item_equip_slot_restriction"
                : restriction.Message);

            if (restriction.Type == SlotRestrictionType.ItemProhibited)
            {
                if (
                    (restriction.Slot == Xml.Objects.EquipmentSlot.Ring && userobj.Equipment.RingEquipped) ||
                    (restriction.Slot == Xml.Objects.EquipmentSlot.Gauntlet && userobj.Equipment.GauntletEquipped) ||
                    (userobj.Equipment[(byte)restriction.Slot] != null)
                )
                {
                    message = restrictionMessage;
                    return false;
                }
            }
            else
            {
                if (
                    (restriction.Slot == Xml.Objects.EquipmentSlot.Ring && !userobj.Equipment.RingEquipped) ||
                    (restriction.Slot == Xml.Objects.EquipmentSlot.Gauntlet && !userobj.Equipment.GauntletEquipped) ||
                    (userobj.Equipment[(byte)restriction.Slot] == null)
                )
                {
                    message = restrictionMessage;
                    return false;
                }
            }
        }

        // Check other equipped item slot restrictions 
        var items = userobj.Equipment.Where(
            predicate: x => x.Template.Properties.Restrictions?.SlotRestrictions != null);
        foreach (var restriction in
                 items.SelectMany(selector: x => x.Template.Properties.Restrictions.SlotRestrictions))
        {
            var restrictionMessage = World.GetLocalString(restriction.Message == string.Empty
                ? "item_equip_slot_restriction"
                : restriction.Message);

            if (restriction.Type == SlotRestrictionType.ItemProhibited)
            {
                if ((restriction.Slot == Xml.Objects.EquipmentSlot.Ring &&
                     EquipmentSlot == (byte)Xml.Objects.EquipmentSlot.LeftHand) ||
                    EquipmentSlot == (byte)Xml.Objects.EquipmentSlot.RightHand ||
                    (restriction.Slot == Xml.Objects.EquipmentSlot.Gauntlet &&
                     EquipmentSlot == (byte)Xml.Objects.EquipmentSlot.LeftArm) ||
                    EquipmentSlot == (byte)Xml.Objects.EquipmentSlot.RightArm || EquipmentSlot == (byte)restriction.Slot)
                {
                    message = restrictionMessage;
                    return false;
                }
            }
            else
            {
                if ((restriction.Slot == Xml.Objects.EquipmentSlot.Ring && userobj.Equipment.LRing != null) ||
                    userobj.Equipment.RRing != null || (restriction.Slot == Xml.Objects.EquipmentSlot.Gauntlet &&
                                                        userobj.Equipment.LGauntlet != null) ||
                    userobj.Equipment.RGauntlet != null || EquipmentSlot != (byte)restriction.Slot)
                {
                    message = restrictionMessage;
                    return false;
                }
            }
        }

        // Check castable requirements
        if (Template.Properties?.Restrictions?.Castables != null)
        {
            var hasCast = false;
            // Behavior is ANY castable, not ALL in list
            foreach (var castable in Template.Properties.Restrictions.Castables)
                if (userobj.SkillBook.IndexOf(castable) != -1 ||
                    userobj.SpellBook.IndexOf(castable) != -1)
                    hasCast = true;

            if (!hasCast && Template.Properties.Restrictions.Castables.Count > 0)
            {
                message = World.GetLocalString("item_equip_missing_castable");
                return false;
            }
        }

        // Check mastership requirement
        if (MasterOnly && !userobj.IsMaster)
        {
            message = World.GetLocalString("item_equip_not_master");
            return false;
        }

        return true;
    }

    public void EvalFormula(Creature source)
    {
        if (Template.Properties?.StatModifiers != null)
            Stats = NumberCruncher.CalculateItemModifiers(this, source);
    }

    public void Invoke(User trigger)
    {
        if (Stackable && Count <= 0)
        {
            trigger.RemoveItem(Name);
            return;
        }

        GameLog.InfoFormat($"User {trigger.Name}: used item {Name}");

        if (Consumable && Template.Properties.StatModifiers != null)
        {
            var statChange = NumberCruncher.CalculateItemModifiers(this, trigger);
            trigger.Stats.Apply(statChange);
            trigger.UpdateAttributes(StatUpdateFlags.Full);
        }

        // Run through all the different potential uses. We allow combinations of any
        // use specified in the item XML.

        if (Use?.Script != null)
        {
            if (!World.ScriptProcessor.TryGetScript(Use.Script, out var invokeScript))
            {
                trigger.SendSystemMessage("It doesn't work.");
                return;
            }

            var env = ScriptEnvironment.Create(("origin", this), ("source", trigger));
            invokeScript.ExecuteFunction("OnUse", env);
        }

        if (Use?.Effect != null) trigger.SendEffect(trigger.Id, Use.Effect.Id, Use.Effect.Speed);

        if (Use?.Sound != null) trigger.SendSound(Use.Sound.Id);

        if (Use?.Teleport != null) trigger.Teleport(Use.Teleport.Value, Use.Teleport.X, Use.Teleport.Y);

        if (Use?.Statuses != null)
        {
            foreach (var add in Use.Statuses.Add)
            {
                if (World.WorldData.TryGetValue<Status>(add.Value.ToLower(), out var applyStatus))
                {
                    var duration = add.Duration == 0 ? applyStatus.Duration : add.Duration;
                    var overlap = trigger.CurrentStatusInfo.Where(x => applyStatus.IsCategory(x.Category)).ToList();
                    if (overlap.Any())
                    {
                        trigger.SendSystemMessage($"You already have an active {overlap.First().Category}.");
                    }
                    else
                    {
                        GameLog.UserActivityInfo(
                            $"Invoke: {trigger.Name} using {Name} - applying status {add.Value} - duration {duration}");
                        trigger.ApplyStatus(new CreatureStatus(applyStatus, trigger, null, null, duration, -1,
                            add.Intensity));
                    }
                }
                else
                {
                    GameLog.UserActivityError(
                        $"Invoke: {trigger.Name} using {Name} - failed to add status {add.Value}, does not exist!");
                }

            }

            foreach (var remove in Use.Statuses.Remove)
            {

                if (World.WorldData.TryGetValue<Status>(remove.ToLower(), out var applyStatus))
                {
                    GameLog.UserActivityError(
                        $"Invoke: {trigger.Name} using {Name} - removing status {remove}");
                    trigger.RemoveStatus(applyStatus.Icon);
                }
                else
                {
                    GameLog.UserActivityError(
                        $"Invoke: {trigger.Name} using {Name} - failed to remove status {remove}, does not exist!");
                }
            }
        }

        if (Procs != null)
        {
            foreach (var proc in Procs.Where(proc =>
                         Random.Shared.NextDouble() <= proc.Chance && proc.Type == ProcEventType.OnUse))
            {
                Game.World.EnqueueProc(proc, null, trigger.Guid, Guid.Empty);
            }
        }

        if (Consumable) Count--;
    }

    public override void ShowTo(IVisible obj)
    {
        if (obj is not User user) return;
        user.SendVisibleItem(this);
    }
}