using Quartz.Core.Enums;

namespace Quartz.Core.Models.BoardEntities;

public abstract class DrawingPrimitive
{
    public PrimitiveType Type { get; init; }
}