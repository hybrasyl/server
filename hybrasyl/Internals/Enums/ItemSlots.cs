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

namespace Hybrasyl.Internals.Enums;

public enum ItemSlots : byte
{
    None = 0,
    Weapon = 1,
    Armor = 2,
    Shield = 3,
    Helmet = 4,
    Earring = 5,
    Necklace = 6,
    LHand = 7,
    RHand = 8,
    LArm = 9,
    RArm = 10,
    Waist = 11,
    Leg = 12,
    Foot = 13,

    // The rest are all "vanity" slots
    FirstAcc = 14,
    Trousers = 15,
    Coat = 16,
    SecondAcc = 17,
    ThirdAcc = 18,

    // These are special edge cases; the slots don't actually exist
    Gauntlet = 19,
    Ring = 20
}