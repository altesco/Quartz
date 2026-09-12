using Quartz.Core.Enums;

namespace Quartz.Core.Models.BoardEntities;

public abstract class Component : BoardDimensionalObject
{
    public string Name { get; set; } = ""; // Уникальное имя (R1, C10, U2)
    public double Value { get; set; } // Номинал (10k, 100nF, 5V)

    public Point2D Point { get; set; } = new();

    public Shape Shape { get; set; } = new RectShape();

    public double Angle { get; set; } = 0.0; // Угол поворота (0, 90, 180, 270)

    public string Footprint { get; set; } = string.Empty; // Тип корпуса (0805, SOIC-8)

    public MountingType MountingType { get; set; } = MountingType.SMD;

    public NameSettings NameSettings { get; set; } = new();
    public List<Pin> Pins { get; set; } = [];
}