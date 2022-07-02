using Hybrasyl.Objects;

namespace Hybrasyl.Interfaces;

public interface IVisible : ISprite
{
    public string Portrait { get; set; }
    public string DisplayText { get; set; }
    public void ShowTo(IVisible target);
    public LocationInfo Location { get; set; }
}
