namespace Quartz.Core.Models.BoardEntities;

public abstract class BoardEntity
{
    // Здесь можно оставить пустое тело или вынести общее свойство Id, если оно есть у всех элементов

    // Позиция элемента в исходном файле YAML
    public long Line { get; set; }
    public long Column { get; set; }
    public long Length { get; set; }
}