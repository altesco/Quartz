namespace Quartz.Core.Models.BoardEntities;

public class Pin : Connection
{
    public override Pin Clone()
    {
        var clone = (Pin)MemberwiseClone();
        clone.Shape = Shape.Clone();
        return clone;
    }
}