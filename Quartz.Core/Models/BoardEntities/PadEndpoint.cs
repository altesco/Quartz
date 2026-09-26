namespace Quartz.Core.Models.BoardEntities;

public class PadEndpoint : EndpointBase
{
    public Component Comp { get; set; } = new Resistor();
    public Pad Pad { get; set; } = new();

    public override PadEndpoint Clone()
    {
        var clone = (PadEndpoint)MemberwiseClone();
        clone.Comp = Comp.Clone();
        clone.Pad = Pad.Clone();
        return clone;
    }
}