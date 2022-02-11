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

using System.Net;
using Hybrasyl.Objects;
using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using StackExchange.Redis;

namespace Hybrasyl;

public class Login : Server
{
    public new LoginPacketHandler[] PacketHandlers { get; private set; }

    public Login(int port, bool isDefault = false)
        : base(port, isDefault)
    {
        GameLog.InfoFormat("LoginConstructor: port is {0}", port);

        PacketHandlers = new LoginPacketHandler[256];

        for (int i = 0; i < 256; ++i)
            PacketHandlers[i] = (c, p) => GameLog.WarningFormat("Login: Unhandled opcode 0x{0:X2}", p.Opcode);

        PacketHandlers[0x02] = PacketHandler_0x02_CreateA;
        PacketHandlers[0x03] = PacketHandler_0x03_Login;
        PacketHandlers[0x04] = PacketHandler_0x04_CreateB;
        PacketHandlers[0x10] = PacketHandler_0x10_ClientJoin;
        PacketHandlers[0x26] = PacketHandler_0x26_ChangePassword;
        PacketHandlers[0x4B] = PacketHandler_0x4B_RequestNotification;
        PacketHandlers[0x68] = PacketHandler_0x68_RequestHomepage;
    }

    public World World { get; private set; }

    private void PacketHandler_0x02_CreateA(Client client, ClientPacket packet)
    {
        var name = packet.ReadString8();
        var password = packet.ReadString8();
        var email = packet.ReadString8();

        // This string will contain a client-ready message if the provided password
        // isn't valid.
        byte passwordErr = 0x0;

        if (World.PlayerExists(name) || (Game.Config.Access?.IsReservedName(name) ?? false))
        {
            client.LoginMessage("That name is unavailable.", 3);
        }
        else if (name.Length < 4 || name.Length > 12)
        {
            client.LoginMessage("Names must be between 4 to 12 characters long.", 3);
        }
        else if (!ValidPassword(password, out passwordErr))
        {
            client.LoginMessage(GetPasswordError(passwordErr), 3);
        }
        else if (Regex.IsMatch(name, "^[A-Za-z]{4,12}$"))
        {
            client.NewCharacterName = name;
            client.NewCharacterPassword = HashPassword(password);
            client.LoginMessage("\0", 0);
        }
        else
        {
            client.LoginMessage("Names may only contain letters.", 3);
        }
    }


    private void PacketHandler_0x03_Login(Client client, ClientPacket packet)
    {
        var name = packet.ReadString8();
        var password = packet.ReadString8();
        GameLog.DebugFormat("cid {0}: Login request for {1}", client.ConnectionId, name);

        if (Game.World.WorldData.TryGetAuthInfo(name, out AuthInfo login))
        {
            if (login.VerifyPassword(password))
            {
                GameLog.DebugFormat("cid {0}: password verified for {1}", client.ConnectionId, name);

                if (login.CurrentState == UserState.Redirect && login.StateChangeDuration < 1000)
                {
                    client.LoginMessage("That character is already logging in. Please try again.", 3);
                    return;
                }

                if (login.IsLoggedIn)
                {
                    login.CurrentState = UserState.Disconnected;
                    // Is the user actually in world?
                    if (Game.World.TryGetActiveUser(login.Username, out _))
                    {
                        GameLog.InfoFormat("cid {0}: {1} logging on again, disconnecting previous connection",
                            client.ConnectionId, name);
                        client.LoginMessage("That character is already online. Please try again.", 3);
                        World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.LogoffUser, name));
                        login.Save();
                        return;
                    }
                }

                // Make sure user can be deserialized without errors
                if (!Game.World.WorldData.TryGetUser(name, out _))
                {
                    // Something bad has happened
                    client.LoginMessage("An unknown error occurred. Please contact Hybrasyl support.", 3);
                    return;
                }
    
                GameLog.DebugFormat("cid {0} ({1}): logging in", client.ConnectionId, name);
                client.LoginMessage("\0", 0);
                client.SendMessage("Welcome to Hybrasyl!", 3);
                GameLog.DebugFormat("cid {0} ({1}): sending redirect to world", client.ConnectionId, name);

                var redirect = new Redirect(client, this, Game.World, name, client.EncryptionSeed,
                    client.EncryptionKey);
                GameLog.InfoFormat("cid {0} ({1}): login successful, redirecting to world server",
                    client.ConnectionId, name);
                login.LastLogin = DateTime.Now;
                login.LastLoginFrom = ((IPEndPoint)client.Socket.RemoteEndPoint).Address.ToString();
                login.CurrentState = UserState.Redirect;
                login.LastStateChange = login.LastLogin;
                login.Save();
                client.Redirect(redirect);
            }
            else
            {
                GameLog.WarningFormat("cid {0} ({1}): password incorrect", client.ConnectionId, name);
                client.LoginMessage("Incorrect password", 3);
                login.LastLoginFailure = DateTime.Now;
                login.LoginFailureCount++;
                login.LastLoginFailureFrom = ((IPEndPoint)client.Socket.RemoteEndPoint).Address.ToString();
                login.CurrentState = UserState.Login;
                login.LastStateChange = login.LastLoginFailure;
                login.Save();
            }
        }
        else
        {
            client.LoginMessage("That character does not exist", 3);
            GameLog.InfoFormat("cid {0}: attempt to login as nonexistent character {1}", client.ConnectionId, name);
            return;
        }
    }

    private void PacketHandler_0x04_CreateB(Client client, ClientPacket packet)
    {
        if (string.IsNullOrEmpty(client.NewCharacterName) || string.IsNullOrEmpty(client.NewCharacterPassword))
        {
            return;
        }
        var hairStyle = packet.ReadByte();
        var gender = packet.ReadByte();
        var hairColor = packet.ReadByte();

        if (hairStyle < 1)
            hairStyle = 1;

        if (hairStyle > 17)
            hairStyle = 17;

        if (hairColor > 13)
            hairColor = 13;

        if (gender < 1)
            gender = 1;

        if (gender > 2)
            gender = 2;

        // Try to get our map
        // TODO: replace with XML config for start map, x, y
        Map map;
        if (!Game.World.WorldData.TryGetValue(136, out map))
            map = Game.World.WorldData.GetDictionary<Map>().First().Value;
        if (!World.PlayerExists(client.NewCharacterName))
        {
            var newPlayer = new User();
            newPlayer.Name = client.NewCharacterName;
            newPlayer.Gender = (Xml.Gender) gender;
            newPlayer.Location.Direction = Xml.Direction.South;
            newPlayer.Location.Map = map;
            newPlayer.Location.X = 10; 
            newPlayer.Location.Y = 10;
            newPlayer.HairColor = hairColor;
            newPlayer.HairStyle = hairStyle;
            newPlayer.Class = Xml.Class.Peasant;
            newPlayer.Gold = 0;
            newPlayer.AuthInfo.CreatedTime = DateTime.Now;
            newPlayer.AuthInfo.FirstLogin = true;
            newPlayer.AuthInfo.PasswordHash = client.NewCharacterPassword;
            newPlayer.AuthInfo.LastPasswordChange = DateTime.Now;
            newPlayer.AuthInfo.LastPasswordChangeFrom = ((IPEndPoint) client.Socket.RemoteEndPoint).Address.ToString();
            newPlayer.AuthInfo.Save();
            newPlayer.Nation = Game.World.DefaultNation;

            IDatabase cache = World.DatastoreConnection.GetDatabase();
            cache.Set(User.GetStorageKey(newPlayer.Name), newPlayer);
            var vault = new Vault(newPlayer.Guid);
            vault.Save();
            var parcelStore = new ParcelStore(newPlayer.Guid);
            parcelStore.Save();
            client.LoginMessage("\0", 0);
        }
    }
 
        
    private void PacketHandler_0x10_ClientJoin(Client client, ClientPacket packet)
    {
        var seed = packet.ReadByte();
        var keyLength = packet.ReadByte();
        var key = packet.Read(keyLength);
        var name = packet.ReadString8();
        var id = packet.ReadUInt32();

        var redirect = ExpectedConnections[id];
        if (Game.World.WorldData.TryGetAuthInfo(name, out AuthInfo login))
        {
            login.CurrentState = UserState.Login;
            login.Save();
        }

        if (redirect.Matches(name, key, seed))
        {
            ((IDictionary)ExpectedConnections).Remove(id);

            client.EncryptionKey = key;
            client.EncryptionSeed = seed;

            if (redirect.Source is Lobby || redirect.Source is World)
            {
                var x60 = new ServerPacket(0x60);
                x60.WriteByte(0x00);
                x60.WriteUInt32(Game.NotificationCrc);
                client.Enqueue(x60);
            }
        }

    }

    // Chart for all error password-related error codes were provided by kojasou@ on
    // https://github.com/hybrasyl/server/pull/11.
    private void PacketHandler_0x26_ChangePassword(Client client, ClientPacket packet)
    {
        var name = packet.ReadString8();
        var currentPass = packet.ReadString8();
        // Clientside validation ensures that the same string is typed twice for the new
        // password, and the new password is only sent to the server once. We can assume
        // that they matched if 0x26 request is sent from the client.
        var newPass = packet.ReadString8();

        if (!Game.World.WorldData.TryGetAuthInfo(name, out AuthInfo login))
        {
            client.LoginMessage(GetPasswordError(0x0E), 0x0E);
            GameLog.InfoFormat("cid {0}: Password change attempt on nonexistent player {1}", client.ConnectionId, name);
            return;
        }

        if (login.VerifyPassword(currentPass))
        {
            // Check if the password is valid.
            if (ValidPassword(newPass, out byte err))
            {
                login.PasswordHash = HashPassword(newPass);
                login.LastPasswordChange = DateTime.Now;
                login.LastPasswordChangeFrom = ((IPEndPoint) client.Socket.RemoteEndPoint).Address.ToString();
                login.Save();
                // Let the user know the good news.
                client.LoginMessage("Your password has been changed successfully.", 0x0);
                GameLog.InfoFormat("Password successfully changed for `{0}`", name);
            }
            else
            {
                client.LoginMessage(GetPasswordError(err), err);
                GameLog.ErrorFormat("Invalid new password proposed during password change attempt for `{0}`", name);
            }
        }
        // The current password is incorrect. Don't allow any changes to happen.
        else
        {
            client.LoginMessage(GetPasswordError(0x0F), 0x0F);
            GameLog.ErrorFormat("Invalid current password during password change attempt for `{0}`", name);
        }
    }
        
    private void PacketHandler_0x4B_RequestNotification(Client client, ClientPacket packet)
    {
        var x60 = new ServerPacket(0x60);
        x60.WriteByte(0x01);
        x60.WriteUInt16((ushort)Game.Notification.Length);
        x60.Write(Game.Notification);
        client.Enqueue(x60);
    }

    private void PacketHandler_0x68_RequestHomepage(Client client, ClientPacket packet)
    {
        var x03 = new ServerPacket(0x66);
        x03.WriteByte(0x03);
        x03.WriteString8("http://www.hybrasyl.com");
        client.Enqueue(x03);
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