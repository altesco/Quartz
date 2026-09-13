using Quartz.Core.Enums;

namespace Quartz.Core.Models.BoardEntities;

public class Pad : Connection
{
    public double DrillDiameter { get; set; } = 0.8; // 0 для чисто SMD
    public bool IsPlated { get; set; } = true; // Металлизация отверстия
    public PinElectricalType ElectricalType { get; set; } = PinElectricalType.Passive;
}