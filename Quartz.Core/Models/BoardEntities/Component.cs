using System.Collections.Frozen;
using Quartz.Core.Enums;

namespace Quartz.Core.Models.BoardEntities;

public abstract class Component : BoardEntity
{
    public string Name { get; set; } = "";
    public double Value { get; set; }

    public Point2D Point { get; set; } = new();

    public Shape Shape { get; set; } = new RectShape { Width = 55, Height = 30 };

    public double Angle { get; set; } = 0.0;

    public Footprint Footprint { get; set; } = new();

    public MountingType MountingType { get; set; } = MountingType.SMD;

    public NameSettings NameSettings { get; set; } = new();
    public IReadOnlyDictionary<string, Pin> Pins { get; set; } = FrozenDictionary<string, Pin>.Empty;
}