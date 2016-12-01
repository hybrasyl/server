using Hybrasyl.Castables;
using Hybrasyl.Objects;

namespace Hybrasyl
{
    public static class Extensions
    {
        public static byte GetMaxLevelForUser(this Castable castable, User user)
        {
            if (user.IsMaster)
            {
                return 100;
            }

            switch (user.Class)
            {
                case Enums.Class.Peasant:
                    return castable.MaxLevel.Peasant;
                case Enums.Class.Warrior:
                    return castable.MaxLevel.Warrior;
                case Enums.Class.Rogue:
                    return castable.MaxLevel.Rogue;
                case Enums.Class.Wizard:
                    return castable.MaxLevel.Wizard;
                case Enums.Class.Priest:
                    return castable.MaxLevel.Priest;
                case Enums.Class.Monk:
                    return castable.MaxLevel.Monk;
                default:
                    return 0;
            }
        }
    }
}
