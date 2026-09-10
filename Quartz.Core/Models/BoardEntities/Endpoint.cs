namespace Quartz.Core.Models.BoardEntities;

public class Endpoint : BoardEntity
{
    public Component Comp { get; set; }
    public Pin Pin { get; set; }
}