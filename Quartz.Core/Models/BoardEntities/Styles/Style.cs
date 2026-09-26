namespace Quartz.Core.Models.BoardEntities.Styles;

public abstract class Style : BoardEntity
{
    public string? Name { get; set; }

    public override abstract Style Clone();
}