namespace Quartz.Core.Models.BoardEntities;

public class Via : Connection
{
    public Net Net { get; init; } = new();

    public LayerModel From { get; init; } = new();
    public LayerModel To { get; init; } = new();

    public double DrillDiameter { get; init; }
}