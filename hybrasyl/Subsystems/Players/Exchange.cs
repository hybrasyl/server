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
using Hybrasyl.Internals.Logging;
using Hybrasyl.Objects;

namespace Hybrasyl.Subsystems.Players;

public class Exchange
{
    private readonly User _source;
    private readonly Inventory _sourceItems;
    private readonly int _sourceSize;
    private readonly User _target;
    private readonly Inventory _targetItems;
    private readonly int _targetSize;
    private bool _active;
    private bool _sourceConfirmed;
    private uint _sourceGold;
    private int _sourceWeight;
    private bool _targetConfirmed;
    private uint _targetGold;
    private int _targetWeight;

    public Exchange(User source, User target)
    {
        _source = source;
        _target = target;
        _sourceItems = new Inventory(60);
        _targetItems = new Inventory(60);
        _sourceGold = 0;
        _targetGold = 0;
        _sourceWeight = 0;
        _targetWeight = 0;
        _sourceSize = source.Inventory.EmptySlots;
        _targetSize = target.Inventory.EmptySlots;
    }

    public bool ConditionsValid =>
        _source.Map == _target.Map && _source.IsInViewport(_target) &&
        _target.IsInViewport(_source) &&
        _source.Condition.InExchange &&
        _target.Condition.InExchange &&
        _active;

    public static bool StartConditionsValid(User source, User target, out string errorMessage)
    {
        errorMessage = string.Empty;
        var locationCheck = source.Map == target.Map && source.IsInViewport(target) &&
                            target.IsInViewport(source) && target.Distance(source) <=
                            Game.ActiveConfiguration.Constants.PlayerExchangeDistance;

        var flagCheck = source.Condition.NoFlags && target.Condition.NoFlags;

        if (!locationCheck)
            errorMessage = "They are too far away.";

        if (!flagCheck)
            errorMessage = "That is not possible now.";

        if (!source.GetClientSetting("exchange"))
            errorMessage = "You have exchange turned off.";

        if (!target.GetClientSetting("exchange"))
            errorMessage = "They do not wish to trade with you.";

        return errorMessage == string.Empty;
    }

    public bool AddItem(User giver, byte slot, byte quantity = 1)
    {
        ItemObject toAdd;
        // Some sanity checks

        // Check if our "exchange" is full
        if (_sourceItems.IsFull || _targetItems.IsFull)
        {
            _source.SendMessage("Maximum exchange size reached. No more items can be added.", MessageTypes.SYSTEM);
            _target.SendMessage("Maximum exchange size reached. No more items can be added.", MessageTypes.SYSTEM);
            return false;
        }

        // Check if either participant's inventory would be full as a result of confirmation
        if (_sourceItems.Count == _sourceSize || _targetItems.Count == _targetSize)
        {
            _source.SendMessage("Inventory full.", MessageTypes.SYSTEM);
            _target.SendMessage("Inventory full.", MessageTypes.SYSTEM);
            return false;
        }

        // Have they already confirmed?
        if (_sourceConfirmed || _targetConfirmed)
            return false;

        // OK - we have room, now what?
        var theItem = giver.Inventory[slot];

        // Further checks!
        // Is the ItemObject exchangeable?

        if (!theItem.Exchangeable)
        {
            giver.SendMessage("You can't trade this.", MessageTypes.SYSTEM);
            return false;
        }

        // Weight check

        if (giver == _source && _targetWeight + theItem.Weight > _target.MaximumWeight)
        {
            _source.SendSystemMessage("It's too heavy.");
            _target.SendSystemMessage("They can't carry any more.");
            return false;
        }

        if (giver == _target && _sourceWeight + theItem.Weight > _source.MaximumWeight)
        {
            _target.SendSystemMessage("It's too heavy.");
            _source.SendSystemMessage("They can't carry any more.");
            return false;
        }

        // Is the ItemObject stackable?

        if (theItem.Stackable && theItem.Count > 1)
        {
            var targetItem = giver == _target
                ? _source.Inventory.FindById(theItem.Name)
                : _target.Inventory.FindById(theItem.Name);

            // Check to see that giver has sufficient number of whatever, and also that the quantity is a positive number
            if (quantity <= 0)
            {
                giver.SendSystemMessage("You can't give zero of something, chief.");
                return false;
            }

            if (quantity > theItem.Count)
            {
                giver.SendSystemMessage($"You don't have that many {theItem.Name} to give!");
                return false;
            }

            // Check if the recipient already has this ItemObject - if they do, ensure the quantity proposed for trade
            // wouldn't put them over maxcount for the ItemObject in question

            if (targetItem != null && targetItem.Count + quantity > theItem.MaximumStack)
            {
                if (giver == _target)
                {
                    _target.SendSystemMessage($"They can't carry any more {theItem.Name}");
                    _source.SendSystemMessage($"You can't carry any more {theItem.Name}.");
                }
                else
                {
                    _source.SendSystemMessage($"They can't carry any more {theItem.Name}");
                    _target.SendSystemMessage($"You can't carry any more {theItem.Name}.");
                }

                return false;
            }

            giver.RemoveItem(theItem.Name, quantity);
            toAdd = new ItemObject(theItem);
            toAdd.Count = quantity;
        }
        else if (!theItem.Stackable || theItem.Count == 1)
        {
            // ItemObject isn't stackable or is a stack of one
            // Remove the ItemObject entirely from giver
            toAdd = theItem;
            giver.RemoveItem(slot);
        }
        else
        {
            GameLog.WarningFormat("exchange: Hijinx occuring: participants are {0} and {1}",
                _source.Name, _target.Name);
            _active = false;
            return false;
        }

        // Now add the ItemObject to the active exchange and make sure we update weight
        if (giver == _source)
        {
            var exchangeSlot = (byte) _sourceItems.Count;
            _sourceItems.AddItem(toAdd);
            _source.SendExchangeUpdate(toAdd, exchangeSlot);
            _target.SendExchangeUpdate(toAdd, exchangeSlot, false);
            _targetWeight += toAdd.Weight;
        }

        if (giver == _target)
        {
            var exchangeSlot = (byte) _targetItems.Count;
            _targetItems.AddItem(toAdd);
            _target.SendExchangeUpdate(toAdd, exchangeSlot);
            _source.SendExchangeUpdate(toAdd, exchangeSlot, false);
            _sourceWeight += toAdd.Weight;
        }

        return true;
    }

    public bool AddGold(User giver, uint amount)
    {
        if (giver == _source)
        {
            if (amount > uint.MaxValue - _sourceGold)
            {
                _source.SendMessage("No more gold can be added to this exchange.", MessageTypes.SYSTEM);
                return false;
            }

            if (amount > _source.Gold)
            {
                _source.SendMessage("You don't have that much gold.", MessageTypes.SYSTEM);
                return false;
            }

            _sourceGold += amount;
            _source.SendExchangeUpdate(amount);
            _target.SendExchangeUpdate(amount, false);
            _source.Stats.Gold -= amount;
            _source.UpdateAttributes(StatUpdateFlags.Experience);
        }
        else if (giver == _target)
        {
            if (amount > uint.MaxValue - _targetGold)
            {
                _target.SendMessage("No more gold can be added to this exchange.", MessageTypes.SYSTEM);
                return false;
            }

            _targetGold += amount;
            _target.SendExchangeUpdate(amount);
            _source.SendExchangeUpdate(amount, false);
            _target.Stats.Gold -= amount;
            _target.UpdateAttributes(StatUpdateFlags.Experience);
        }
        else
        {
            return false;
        }

        return true;
    }

    public bool StartExchange()
    {
        GameLog.InfoFormat("Starting exchange between {0} and {1}", _source.Name, _target.Name);
        _active = true;
        _source.Condition.InExchange = true;
        _target.Condition.InExchange = true;
        _source.ActiveExchange = this;
        _target.ActiveExchange = this;
        // Send "open window" packet to both clients
        _target.SendExchangeInitiation(_source);
        _source.SendExchangeInitiation(_target);
        return true;
    }

    /// <summary>
    ///     Cancel the exchange, returning all items from the window back to each player.
    /// </summary>
    /// <returns>Boolean indicating success. Better hope this is always true.</returns>
    public bool CancelExchange(User requestor)
    {
        foreach (var item in _sourceItems) _source.AddItem(item);
        foreach (var item in _targetItems) _target.AddItem(item);
        _source.AddGold(_sourceGold);
        _target.AddGold(_targetGold);
        _source.SendExchangeCancellation(requestor == _source);
        _target.SendExchangeCancellation(requestor == _target);
        _source.ActiveExchange = null;
        _target.ActiveExchange = null;
        _source.Condition.InExchange = false;
        _target.Condition.InExchange = false;
        return true;
    }

    /// <summary>
    ///     Perform the exchange once confirmation from both sides is received.
    /// </summary>
    /// <returns></returns>
    public void PerformExchange()
    {
        GameLog.Info("Performing exchange");
        foreach (var item in _sourceItems) _target.AddItem(item);
        foreach (var item in _targetItems) _source.AddItem(item);
        _source.AddGold(_targetGold);
        _target.AddGold(_sourceGold);

        _source.ActiveExchange = null;
        _target.ActiveExchange = null;
        _source.Condition.InExchange = false;
        _target.Condition.InExchange = false;
    }

    /// <summary>
    ///     Confirm the exchange. Once both sides confirm, perform the exchange.
    /// </summary>
    /// <returns>Boolean indicating success.</returns>
    public void ConfirmExchange(User requestor)
    {
        if (_source == requestor)
        {
            GameLog.InfoFormat("Exchange: source ({0}) confirmed", _source.Name);
            _sourceConfirmed = true;
            _target.SendExchangeConfirmation(false);
        }

        if (_target == requestor)
        {
            GameLog.InfoFormat("Exchange: target ({0}) confirmed", _target.Name);
            _targetConfirmed = true;
            _source.SendExchangeConfirmation(false);
        }

        if (_sourceConfirmed && _targetConfirmed)
        {
            GameLog.Info("Exchange: Both sides confirmed");
            _source.SendExchangeConfirmation();
            _target.SendExchangeConfirmation();
            PerformExchange();
        }
    }
}