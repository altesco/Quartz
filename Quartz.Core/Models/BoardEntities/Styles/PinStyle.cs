namespace Quartz.Core.Models.BoardEntities.Styles;

public class PinStyle : ConnectionStyle
{
    public override PinStyle Clone()
    {
        var clone = (PinStyle)MemberwiseClone();
        CopyConnectionStylePropertiesTo(clone);
        return clone;
    }
}