namespace Quartz.Core.Models.BoardEntities;

public abstract class BoardEntity
{
    public long Line { get; set; }
    public long Column { get; set; }
    public long Length { get; set; }

    public abstract BoardEntity Clone();
}