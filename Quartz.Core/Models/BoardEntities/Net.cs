namespace Quartz.Core.Models.BoardEntities;

public class Net : BoardEntity
{
    public string Name { get; set; } = "";

    public List<PadEndpoint> Nodes { get; set; } = [];

    public override Net Clone()
    {
        var clone = (Net)MemberwiseClone();
        clone.Nodes = Nodes.Select(n => n.Clone()).ToList();
        return clone;
    }
}