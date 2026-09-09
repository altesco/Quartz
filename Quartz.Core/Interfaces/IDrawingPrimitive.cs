using Quartz.Core.Enums;

namespace Quartz.Core.Interfaces;

public interface IDrawingPrimitive
{
    PrimitiveType Type { get; }
}