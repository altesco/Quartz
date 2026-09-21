namespace Quartz.Core.Models.BoardEntities;

public class Net : BoardEntity
{
    public string Name { get; set; } = "";

    public List<PadEndpoint> Nodes { get; init; } = [];
}