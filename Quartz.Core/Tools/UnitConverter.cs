using Quartz.Core.Enums;
using Quartz.Core.Models.BoardEntities;

namespace Quartz.Core.Tools;

public static class UnitConverter
{
    public static double ToMillimeters(this double value, LengthUnit unit) => unit switch
    {
        LengthUnit.Cm => value * 10.0,
        LengthUnit.Mm => value,
        LengthUnit.Um => value / 1_000.0,
        LengthUnit.Nm => value / 1_000_000.0,

        LengthUnit.Ft => value * 304.8,
        LengthUnit.In => value * 25.4,
        LengthUnit.Mil => value * 0.0254,

        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
    };

    public static Point2D ToMillimeters(this Point2D point, LengthUnit unit)
        => new(point.X.ToMillimeters(unit), point.Y.ToMillimeters(unit));
}