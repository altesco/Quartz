namespace Quartz.Core.Models.BoardEntities;

public class PadEndpoint : EndpointBase
{
    public Component Comp { get; set; } = new Resistor();
    public Pad Pad { get; set; } = new();
}