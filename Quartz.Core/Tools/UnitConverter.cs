using Quartz.Core.Enums;
using Quartz.Core.Models.BoardEntities;

namespace Quartz.Core.Tools;

public static class UnitConverter
{
    public static double ToMillimeters(this double value, LengthUnit unit) => unit switch
    {
        LengthUnit.Centimeter => value * 10.0,
        LengthUnit.Millimeter => value,
        LengthUnit.Micrometer => value / 1_000.0,
        LengthUnit.Nanometer => value / 1_000_000.0,

        LengthUnit.Foot => value * 304.8,
        LengthUnit.Inch => value * 25.4,
        LengthUnit.Mil => value * 0.0254,

        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
    };

    public static Point2D ToMillimeters(this Point2D point, LengthUnit unit)
        => new(point.X.ToMillimeters(unit), point.Y.ToMillimeters(unit));
}