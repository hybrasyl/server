using Hybrasyl.Objects;


namespace Hybrasyl.Messaging
{
    class GroupCommand : ChatCommand
    {
        public new static string Command = "group";
        public new static string ArgumentText = "<string username>";
        public new static string HelpText = "Invite the specified player to your group.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (!Game.World.TryGetActiveUser(args[0], out User newMember))
                return Fail($"The user {args[0]} could not be found");
            user.InviteToGroup(newMember);
            return Success($"{args[0]} invited to your group.");
        }
    }

    class UngroupCommand : ChatCommand
    {
        public new static string Command = "ungroup";
        public new static string ArgumentText = "none";
        public new static string HelpText = "Leave your group.";
        public new static bool Privileged = false;

        public new static ChatCommandResult Run(User user, params string[] args)
        {
            if (user.Group != null)
            {
                user.Group.Remove(user);
                return Success("You have left the group.");
            }
            return Fail("You are not in a group");
        }

    }
}
