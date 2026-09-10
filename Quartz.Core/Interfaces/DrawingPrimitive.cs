using Quartz.Core.Enums;

namespace Quartz.Core.Interfaces;

public abstract class DrawingPrimitive
{
    public PrimitiveType Type { get; init; }
}