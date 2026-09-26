using Quartz.Core.Enums;

namespace Quartz.Core.Models.BoardEntities.Styles;

public class PadStyle : ConnectionStyle
{
    public double? DrillDiameter { get; set; }
    public bool? IsPlated { get; set; }
    public PinElectricalType? ElectricalType { get; set; }

    public override PadStyle Clone()
    {
        var clone = (PadStyle)MemberwiseClone();
        CopyConnectionStylePropertiesTo(clone);
        return clone;
    }
}