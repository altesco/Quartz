namespace Quartz.Core.Models.BoardEntities;

public class Footprint : BoardEntity //BoardDimensionalObject
{
    public List<Pad> Pads { get; set; } = [];
    public Shape Shape { get; set; } = new RectShape();
}