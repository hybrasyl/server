using Grpc.Core;
using Hybrasyl.Objects;
using System;
using System.Threading.Tasks;
using Hybrasyl;

namespace HybrasylGrpc
{
    class PatronServer : Patron.PatronBase
    {
        public override Task<AuthReply> Auth(AuthRequest request, ServerCallContext context)
        {
            try
            {
                if (World.TryGetUser(request.Username, out User user))
                {
                    if (user.VerifyPassword(request.Password))
                        return Task.FromResult(new AuthReply() { Message = "", Success = true });
                }
                else
                    return Task.FromResult(new AuthReply() { Message = "Authentication failed", Success = false });
            }
            catch (Exception e)
            {
                GameLog.UserActivityError("grpc: Auth: Unknown exception {e}", e);
            }
            return Task.FromResult(new AuthReply() { Message = "Unknown error", Success = false });
        }

        public override Task<ResetPasswordReply> ResetPassword(ResetPasswordRequest request, ServerCallContext context)
        {
            try
            {
                // Simple length check
                if (request.NewPassword.Length > 8 || request.NewPassword.Length < 4)
                    return Task.FromResult(new ResetPasswordReply() { Message = "Passwords must be between 4 and 8 characters", Success = false });

                if (Game.World.UserConnected(request.Username))
                    return Task.FromResult(new ResetPasswordReply() { Message = "User is currently logged in", Success = false });

                if (World.TryGetUser(request.Username, out User user))
                {
                    user.Password.Hash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword,
                        BCrypt.Net.BCrypt.GenerateSalt(12));
                    user.Password.LastChanged = DateTime.Now;
                    user.Password.LastChangedFrom = context.Peer;
                    user.Save();
                }
                else
                    return Task.FromResult(new ResetPasswordReply() { Message = "Unknown user", Success = false });
            }
            catch (Exception e)
            {
                GameLog.UserActivityError("grpc: ResetPassword: unknown exception {e}", e);
            }
            return Task.FromResult(new ResetPasswordReply() { Message = "Unknown error", Success = false });
        }
    }
}
