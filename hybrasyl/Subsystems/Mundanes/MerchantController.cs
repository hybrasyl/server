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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hybrasyl.Internals.Enums;
using Hybrasyl.Objects;
using Hybrasyl.Subsystems.Messaging;
using Hybrasyl.Subsystems.Messaging.ChatCommands;
using Hybrasyl.Xml.Objects;

namespace Hybrasyl.Subsystems.Mundanes;

public class MerchantController
{
    private readonly Merchant Merchant;

    // TODO: static?
    private readonly Dictionary<Regex, MerchantCommand> Triggers = new();

    public MerchantController(Merchant merchant)
    {
        Merchant = merchant;
        foreach (var method in typeof(MerchantController).GetMethods())
        {
            var attr = method.GetCustomAttribute<RegexTrigger>();
            if (attr == null) continue;
            var regex = new Regex(attr.Trigger, RegexOptions.IgnoreCase);
            var job = MerchantJob.None;
            var action = (Action<MerchantControllerRequest>) Delegate.CreateDelegate(
                typeof(Action<MerchantControllerRequest>), this, method, false);
            var jobAttr = method.GetCustomAttribute<MerchantRequiredJob>();
            if (jobAttr != null)
                job = jobAttr.Job;
            Triggers.Add(regex, new MerchantCommand(action, job));
        }
    }

    public static string Pluralize(uint amount)
    {
        return amount switch
        {
            0 => "no coins",
            1 => "1 coin",
            > 1 => $"{amount} coins"
        };
    }

    public static string Verb(uint amount) => amount == 1 ? "is" : "are";


    public bool Evaluate(SpokenEvent e)
    {
        foreach (var (key, value) in Triggers)
        {
            var match = key.Match(e.Message);
            if (!match.Success) continue;
            if (value.RequiredJob != MerchantJob.None && !Merchant.Jobs.HasFlag(value.RequiredJob))
            {
                Merchant.Say("Sorry, I can't do that.");
                return true;
            }

            value.Action(new MerchantControllerRequest(e.Speaker, match.Groups));
            return true;
        }

        return false;
    }

    [RegexTrigger(@"how many (?<item>.*) do i have (deposited|on deposit)")]
    [MerchantRequiredJob(MerchantJob.Bank)]
    public void QuantityDeposited(MerchantControllerRequest request)
    {
        if (request.Speaker is not User user) return;

        if (user.Vault.Items.TryGetValue(request.Match["item"].Value, out var qty))
        {
            Merchant.Say($"You have {qty} of those deposited.");
            return;
        }

        Merchant.Say("You have none of those deposited.");
    }

    [RegexTrigger(@"how much gold do i have (deposited|on deposit)")]
    [MerchantRequiredJob(MerchantJob.Bank)]
    public void GoldOnDeposit(MerchantControllerRequest request)
    {
        if (request.Speaker is not User user) return;
        Merchant.Say($"You have {Pluralize(user.Vault.CurrentGold)} on deposit.");
    }

    [RegexTrigger(@"buy\s+(?<amt>\d+|all)\s+of\s+my\s+(?<target>.*)")]
    [MerchantRequiredJob(MerchantJob.Vend)]
    public void Buy(MerchantControllerRequest request)
    {
        if (request.Speaker is not User user) return;

        var items = Game.World.WorldData.FindItem(request.Match["target"].Value).ToList();
        // Is the thing a category or an actual item?
        if (items.Count != 0)
        {
            // Support both "buy 3 of my <item> and buy all of my <item>"
            if (request.Match["amt"].Value.ToLower() == "all")
            {
                uint coins = 0;
                var removed = 0;
                foreach (var slot in user.Inventory.GetSlotsByName(request.Match["target"].Value))
                {
                    coins += (uint) Math.Round(user.Inventory[slot].Value * user.Inventory[slot].Count *
                                               Game.ActiveConfiguration.Constants.MerchantBuybackPercentage);
                    removed += user.Inventory[slot].Count;
                    user.RemoveItem(slot);
                }

                if (removed > 0)
                {
                    Merchant.Say($"Certainly. I will buy {removed} of those for {Pluralize(coins)}, {user.Name}.");
                    user.Stats.Gold += coins;
                    user.UpdateAttributes(StatUpdateFlags.Experience);
                }
                else
                {
                    user.SendSystemMessage($"\"Sorry, {user.Name},\" {Merchant.Name} says. \"You don't have those!\"");
                }
            }
            else if (int.TryParse(request.Match["amt"].Value, out var qty))
            {
                if (user.Inventory.ContainsName(request.Match["target"].Value, qty))
                {
                    // Deal with annoying duplicate name / different gender edge cases
                    var actuallyRemoved = new List<(byte Slot, int Quantity)>();
                    uint coins = 0;
                    foreach (var item in items)
                    {
                        user.Inventory.TryRemoveQuantity(item.Id, out var removed, qty);
                        actuallyRemoved.AddRange(removed);
                        coins += (uint) (removed.Sum(selector: x => x.Quantity) * item.Properties.Physical.Value);
                    }

                    if (actuallyRemoved.Count > 0)
                    {
                        foreach (var (slot, _) in actuallyRemoved)
                            if (user.Inventory[slot] == null)
                                user.SendClearItem(slot);
                            else
                                user.SendItemUpdate(user.Inventory[slot], slot);
                        Merchant.Say(
                            $"Certainly. I will buy {actuallyRemoved.Sum(selector: x => x.Quantity)} of those for {Pluralize(coins)}, {user.Name}.");
                        user.Stats.Gold += coins;
                        user.UpdateAttributes(StatUpdateFlags.Experience);
                    }
                    else
                    {
                        user.SendSystemMessage(
                            $"\"Sorry, {user.Name},\" {Merchant.Name} says. \"You don't have those!\"");
                    }
                }
                else
                {
                    user.SendSystemMessage($"\"Sorry, {user.Name},\" {Merchant.Name} says. \"You don't have those!\"");
                }
            }
        }
        else if (Game.World.WorldData.GetStore<Item>().ContainsCategory(request.Match["target"].Value))
        {
            uint coins = 0;
            // Only support "buy all my <category>"
            foreach (var slot in user.Inventory.GetSlotsByCategory(request.Match["target"].Value))
            {
                coins += (uint) (user.Inventory[slot].Value * user.Inventory[slot].Count);
                user.RemoveItem(slot);
            }

            if (coins == 0)
            {
                Merchant.Say("You don't seem to have any of those.");
                return;
            }

            Merchant.Say($"Certainly. That will be {Pluralize(coins)}, {user.Name}.");
            user.Stats.Gold += coins;
            user.UpdateAttributes(StatUpdateFlags.Experience);
        }
        else
        {
            user.SendSystemMessage($"{Merchant.Name} shrugs. \"Sorry, I don't know what you mean.\"");
        }
    }


    [RegexTrigger(@"repair all")]
    [MerchantRequiredJob(MerchantJob.Repair)]
    public void RepairAll(MerchantControllerRequest request)
    {
        if (request.Speaker is not User user) return;
        var repairTotal = 0;
        foreach (var item in user.Inventory)
        {
            if (item.MaximumDurability == 0 || item.Durability == item.MaximumDurability) continue;
            if (item.RepairCost > user.Stats.Gold)
            {
                Merchant.Say($"You'll need {item.RepairCost} more gold to repair all of it, I'm afraid.");
                return;
            }

            item.Durability = item.MaximumDurability;
            user.Stats.Gold -= item.RepairCost;
            repairTotal += (int) item.RepairCost;
        }

        foreach (var item in user.Equipment)
        {
            if (item.MaximumDurability == 0 || item.Durability == item.MaximumDurability) continue;
            if (item.RepairCost > user.Stats.Gold)
            {
                Merchant.Say($"You'll need {item.RepairCost} more gold to repair all of it, I'm afraid.");
                return;
            }

            item.Durability = item.MaximumDurability;
            user.Stats.Gold -= item.RepairCost;
            repairTotal += (int) item.RepairCost;
        }

        if (repairTotal > 0)
        {
            Merchant.Say($"I repaired it all for {Pluralize((uint) repairTotal)}.");
            user.SendInventory();
            user.SendEquipment();
            user.UpdateAttributes(StatUpdateFlags.Full);
            return;
        }

        Merchant.Say("Nothing needed to be repaired.");
    }

    [RegexTrigger(@"repair my (?<item>.*)")]
    [MerchantRequiredJob(MerchantJob.Repair)]
    public void RepairItem(MerchantControllerRequest request)
    {
        if (request.Speaker is not User user) return;
        List<(byte slot, ItemObject obj)> slotList;
        if (!user.Inventory.TryGetValueByName(request.Match["item"].Value, out slotList) &&
            !user.Equipment.TryGetValueByName(request.Match["item"].Value, out slotList))
        {
            Merchant.Say("You don't seem to have one of those.");
            return;
        }

        foreach (var (slot, obj) in slotList)
        {
            if (obj.MaximumDurability == 0 || obj.Durability == obj.MaximumDurability) continue;
            if (obj.RepairCost > user.Stats.Gold)
            {
                Merchant.Say($"You'll need {obj.RepairCost} more gold to repair that, I'm afraid.");
                return;
            }

            Merchant.Say($"I repaired your {obj.Name} for {obj.RepairCost} coins.");

            obj.Durability = obj.MaximumDurability;
            user.Stats.Gold -= obj.RepairCost;
            user.SendInventorySlot(slot);
        }
    }

    [RegexTrigger(@"deposit (?<amount>\d+) (coins|gold|coin)")]
    [MerchantRequiredJob(MerchantJob.Bank)]
    public void DepositGold(MerchantControllerRequest request)
    {
        if (request.Speaker is not User user) return;

        if (!uint.TryParse(request.Match["amount"].Value, out var gold))
        {
            Merchant.Say("I don't know what you mean.");
            return;
        }

        if (gold > user.Stats.Gold)
        {
            Merchant.Say("You don't have that much.");
            return;
        }

        if (user.Vault.AddGold(gold))
        {
            Merchant.Say($"I'll take your {Pluralize(gold)}.");
            user.Stats.Gold -= gold;
            user.UpdateAttributes(StatUpdateFlags.Experience);
            return;
        }

        Merchant.Say("Sorry, you can't deposit any more gold.");
    }

    [RegexTrigger(@"deposit (?<item>.*)")]
    [MerchantRequiredJob(MerchantJob.Bank)]
    public void DepositItem(MerchantControllerRequest request)
    {
        if (request.Speaker is not User user) return;
        List<(byte slot, ItemObject obj)> slot;
        if (!user.Inventory.TryGetValueByName(request.Match["item"].Value, out slot))
        {
            Merchant.Say("You don't seem to have one of those.");
            return;
        }

        // Even if multiple results are returned, use the first one
        var first = slot.First();
        var fee = (uint) Math.Round(first.obj.Value * 0.10, 0);

        if (!first.obj.Depositable || first.obj.Bound)
        {
            Merchant.Say("No. I don't even want to touch it.");
            return;
        }

        if (first.obj.Durability != 0 && first.obj.Durability != first.obj.MaximumDurability)
        {
            Merchant.Say("I don't want your junk. Ask a smith to fix it.");
            return;
        }

        if (fee > user.Stats.Gold)
        {
            Merchant.Say($"I'll need {Pluralize(fee)} to deposit that.");
            return;
        }

        if (user.Vault.IsFull)
        {
            Merchant.Say("I can't take any more of your items.");
            return;
        }


        if (user.Inventory[first.slot].Stackable && user.Inventory[first.slot].Count > 1)
            user.RemoveItem(first.obj.Name);
        else
            user.RemoveItem(first.slot);

        user.RemoveGold(fee);
        Game.World.Remove(first.obj);
        user.Vault.AddItem(first.obj.Name);
        user.Vault.Save();
        Merchant.Say($"{first.obj.Name}, that'll be {Pluralize(fee)}.");
    }

    [RegexTrigger(@"withdraw (?<amt>\d+) (coins|gold|coin)")]
    [MerchantRequiredJob(MerchantJob.Bank)]
    public void WithdrawGold(MerchantControllerRequest request)
    {
        if (request.Speaker is not User user) return;
        if (!uint.TryParse(request.Match["amt"].Value, out var gold))
        {
            Merchant.Say("I don't know what you mean.");
            return;
        }

        if (!user.Vault.RemoveGold(gold))
        {
            Merchant.Say("Sorry, you don't have that much on deposit.");
            return;
        }

        user.Stats.Gold += gold;
        user.Vault.RemoveGold(gold);
        user.UpdateAttributes(StatUpdateFlags.Experience);
        Merchant.Say($"Here {Verb(gold)} your {Pluralize(gold)}.");
    }

    [RegexTrigger(@"withdraw (?<item>.*)")]
    [MerchantRequiredJob(MerchantJob.Bank)]
    public void WithdrawItem(MerchantControllerRequest request)
    {
        if (request.Speaker is not User user) return;

        if (!user.Vault.Items.ContainsKey(request.Match["item"].Value))
        {
            Merchant.Say("You don't have any of those.");
            return;
        }

        if (!Game.World.WorldData.TryGetValueByIndex(request.Match["item"].Value, out Item item))
        {
            Merchant.Say("I don't know what that is.");
            return;
        }

        var match = user.Inventory.GetSlotsByName(request.Match["item"].Value);
        if (!match.Any() && user.Inventory.IsFull)
        {
            Merchant.Say("Sorry, you can't hold any more of those.");
            return;
        }

        foreach (var slot in match)
            if (user.Inventory[slot].Stackable && user.Inventory[slot].Count != user.Inventory[slot].MaximumStack)
            {
                user.Inventory[slot].Count++;
                user.SendInventorySlot(slot);
                Merchant.Say($"Here's your {user.Inventory[slot].Name} back.");
                return;
            }

        if (user.Inventory.IsFull)
        {
            Merchant.Say("Sorry, you can't hold any more of those.");
            return;
        }

        user.Vault.RemoveItem(item.Name);
        var itemObj = new ItemObject(item, user.World.Guid);
        itemObj.Durability = itemObj.MaximumDurability;
        var newSlot = user.Inventory.FindEmptySlot();
        user.Inventory[newSlot] = itemObj;
        user.SendInventorySlot(newSlot);
        Merchant.Say($"Here's your {itemObj.Name} back.");
    }
}