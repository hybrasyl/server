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

using System.Collections.Generic;
using Hybrasyl.Internals.Enums;

namespace Hybrasyl.Networking.ServerPackets;

internal class Manufacture
{
    private readonly byte OpCode;

    internal Manufacture()
    {
        OpCode = OpCodes.Manufacture;
    }

    public bool IsInitial { get; set; }
    public byte RecipeCount { get; set; }
    public byte Index { get; set; }
    public ushort Sprite { get; set; }
    public string RecipeName { get; set; }
    public string RecipeDescription { get; set; }
    public Dictionary<string, int> RecipeIngredients { get; set; }

    internal ServerPacket Packet()
    {
        var packet = new ServerPacket(OpCode);
        packet.WriteByte(0x01);
        packet.WriteByte(0x3C);
        if (IsInitial)
        {
            packet.WriteByte(0x00);
            packet.WriteByte(RecipeCount);
            packet.WriteByte(0x00);
        }
        else
        {
            packet.WriteByte(0x01);
            packet.WriteByte(Index);
            packet.WriteUInt16(Sprite);

            packet.WriteString16(RecipeDescription);

            var ing = "Ingredients: \n";
            foreach (var ingredient in RecipeIngredients) ing += $"{ingredient.Value} {ingredient.Key}\n";
            packet.WriteString16(ing);
            packet.WriteByte(0x01);
            packet.WriteByte(0x00);
        }

        return packet;
    }
}