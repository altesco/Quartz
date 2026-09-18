namespace Quartz.Core.Models.BoardEntities;

public class Net : BoardEntity
{
    public string Name { get; init; } = "";

    public List<Endpoint> Nodes { get; init; } = [];
}