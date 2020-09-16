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

using Grpc.Core;
using Hybrasyl.Objects;
using System;
using System.Threading.Tasks;
using Hybrasyl;
using Google.Protobuf.WellKnownTypes;
using System.Linq;

namespace HybrasylGrpc
{
    class PatronServer : Patron.PatronBase
    {
        public override Task<BooleanMessageReply> BeginShutdown(BeginShutdownRequest request, ServerCallContext context)
        {
            var resp = new BooleanMessageReply
            {
                Message = "An unknown error occurred",
                Success = false
            };
            try
            {
                if (!World.ControlMessageQueue.IsAddingCompleted)
                {
                    World.ControlMessageQueue.Add(new HybrasylControlMessage(ControlOpcodes.ShutdownServer, 
                        context.Peer, request.Delay));
                    resp.Message = "Shutdown request successfully submitted";
                    resp.Success = true;
                }
                else
                    resp.Message = "Control message queue closed (shutdown already in progress?)";
            }
            catch (Exception e)
            {
                Game.ReportException(e);
                GameLog.Error("GRPC: Shutdown request failed, {e}", e);
            }
            return Task.FromResult(resp);
        }

        public override Task<UserCountReply> TotalUserCount(Empty empty, ServerCallContext context)
        {
            try
            {
                return Task.FromResult(new UserCountReply() { Number = Game.World.ActiveUsers.Count() });
            }
            catch (Exception e)
            {
                Game.ReportException(e);
                GameLog.Error("GRPC: UserCount failed, {e}", e);
            }
            return Task.FromResult(new UserCountReply() { Number = -1 });
        }

        public override Task<BooleanMessageReply> IsShutdownComplete(Empty empty, ServerCallContext context)
        {
            var resp = new BooleanMessageReply();
            resp.Message = "Hybrasyl active, no shutdown in progress";
            resp.Success = false;

            if (Game.ShutdownComplete)
            {
                resp.Message = "Shutdown complete";
                resp.Success = true;
            }
            else if (Game.ShutdownTimeRemaining > 0)
            {
                resp.Message = $"Shutdown will complete in {Game.ShutdownTimeRemaining} seconds";
            }
            else if (!Game.IsActive())
                resp.Message = "Shutdown is in progress";

            return Task.FromResult(resp);
        }

        public override Task<BooleanMessageReply> Auth(AuthRequest request, ServerCallContext context)
        {
            try
            {
                if (World.TryGetUser(request.Username, out User user))
                {
                    if (user.VerifyPassword(request.Password))
                        return Task.FromResult(new BooleanMessageReply() { Message = "", Success = true });
                }
                else
                    return Task.FromResult(new BooleanMessageReply() { Message = "Authentication failed", Success = false });
            }
            catch (Exception e)
            {
                GameLog.UserActivityError("grpc: Auth: Unknown exception {e}", e);
            }
            return Task.FromResult(new BooleanMessageReply() { Message = "Unknown error", Success = false });
        }

        public override Task<BooleanMessageReply> ResetPassword(ResetPasswordRequest request, ServerCallContext context)
        {
            try
            {
                // Simple length check
                if (request.NewPassword.Length > 8 || request.NewPassword.Length < 4)
                    return Task.FromResult(new BooleanMessageReply() { Message = "Passwords must be between 4 and 8 characters", 
                        Success = false });

                if (Game.World.UserConnected(request.Username))
                    return Task.FromResult(new BooleanMessageReply() { Message = "User is currently logged in", 
                        Success = false });

                if (World.TryGetUser(request.Username, out User user))
                {
                    user.Password.Hash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword,
                        BCrypt.Net.BCrypt.GenerateSalt(12));
                    user.Password.LastChanged = DateTime.Now;
                    user.Password.LastChangedFrom = context.Peer;
                    user.Save();
                }
                else
                    return Task.FromResult(new BooleanMessageReply() { Message = "Unknown user", Success = false });
            }
            catch (Exception e)
            {
                GameLog.UserActivityError("grpc: ResetPassword: unknown exception {e}", e);
            }
            return Task.FromResult(new BooleanMessageReply() { Message = "Unknown error", Success = false });
        }
    }
}
