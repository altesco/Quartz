using Quartz.Core.Enums;
using Quartz.Core.Models.BoardEntities;

namespace Quartz.Core.Models;

public class LayerModel
{
    public Shape Shape { get; set; } = new RectShape();

    public LengthUnit Unit { get; set; } = LengthUnit.Millimeter;

    public List<Component> Components { get; set; } = [];
    public List<Trace> Traces { get; set; } = [];
}