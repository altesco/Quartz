namespace Quartz.Core.Models.BoardEntities;

public class Endpoint : BoardEntity
{
    public Component Comp { get; set; }
    public Pad Pad { get; set; }
}