namespace Quartz.Core.Models.BoardEntities;

public class Via : Connection
{
    public Net Net { get; set; } = new();

    public List<LayerModel> Layers { get; set; } = [];

    public double DrillDiameter { get; set; }

    public override Via Clone()
    {
        var clone = (Via)MemberwiseClone();
        clone.Shape = Shape.Clone();
        clone.Net = Net.Clone();
        clone.Layers = Layers.ToList();
        return clone;
    }
}