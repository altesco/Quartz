namespace Quartz.Core.Models.BoardEntities;

public class Net : BoardEntity
{
    public List<Endpoint> Nodes { get; init; } = [];
}