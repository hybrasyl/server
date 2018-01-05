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
 * (C) 2013 Justin Baugh (baughj@hybrasyl.com)
 * (C) 2015 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Justin Baugh  <baughj@hybrasyl.com>
 *            Kyle Speck    <kojasou@hybrasyl.com>
 */

using System.Net;
using Hybrasyl.Enums;
using Hybrasyl.Objects;
using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Hybrasyl
{

    public partial class Login : Server
    {
        public World World { get; private set; }

        public new LoginPacketHandler[] PacketHandlers { get; private set; }

        public Login(int port)
            : base(port)
        {
            Logger.InfoFormat("LoginConstructor: port is {0}", port);

            PacketHandlers = new LoginPacketHandler[256];

            for (int i = 0; i < 256; ++i)
                PacketHandlers[i] = (c, p) => Logger.WarnFormat("Login: Unhandled opcode 0x{0:X2}", p.Opcode);

            SetPacketHandlers();
        }

        /**
         * Hashes the provided password and returns the hashed version. This method should be used
         * any time a raw password is being stored.
         */
        private string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
        }

        /**
         * Checks that a string is a valid password.
         *
         * TODO: can we modernize this policy to allow for better passwords?
         */
        private bool ValidPassword(string password, out byte code)
        {
            // Examples: aaa, ReallyLongPassword
            if (password.Length < 4 || password.Length > 8)
            {
                code = 0x05;
                return false;
            }

            // Examples: 12345, 84943
            if (password.Length < 6 && password.All(Char.IsDigit))
            {
                code = 0x07;
                return false;
            }

            // Examples: aaaaaa, 111111
            if (password.Distinct().Count() < 3)
            {
                code = 0x08;
                return false;
            }

            // Examples: party@11, temP.49
            Regex r = new Regex("^[a-zA-Z0-9]*$");
            if (!r.IsMatch(password))
            {
                code = 0x09;
                return false;
            }

            // Currently not returning 0x0A; this doesn't seem to be particularly useful
            // and would either need to be a hard-coded blacklist or very naive.

            code = 0x00;
            return true;
        }

        private string GetPasswordError(byte code)
        {
            // If there was an error, find out what we should tell the user.
            switch (code)
            {
                case 0x05:
                    return "The password must be between 4 and 8 characters.";
                case 0x07:
                    return "That numeric password is too short.";
                case 0x08:
                    return "That password is too simple.";
                case 0x09:
                    return "That password has invalid characters.";
                case 0x0A:
                    return "That password is too easy to guess.";
                case 0x0E:
                    return "That name does not exist.";
                case 0x0F:
                    return "Incorrect password.";
                    // This shouldn't happen but who knows.
                default:
                    return "Unknown error; please try changing to another password.";
            }
        }
    }
}
