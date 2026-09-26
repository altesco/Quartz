namespace Quartz.Core.Models.BoardEntities.Styles;

public class ViaStyle : ConnectionStyle
{
    public double? DrillDiameter { get; set; }

    public override ViaStyle Clone()
    {
        var clone = (ViaStyle)MemberwiseClone();
        CopyConnectionStylePropertiesTo(clone);
        return clone;
    }
}