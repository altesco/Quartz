using Quartz.Core.Enums;

namespace Quartz.Core.Models.BoardEntities;

public class Pad : Connection
{
    public double DrillDiameter { get; set; } = 0.8;
    public bool IsPlated { get; set; } = true;
    public PinElectricalType ElectricalType { get; set; } = PinElectricalType.Passive;

    public override Pad Clone()
    {
        var clone = (Pad)MemberwiseClone();
        clone.Shape = Shape.Clone();
        return clone;
    }
}