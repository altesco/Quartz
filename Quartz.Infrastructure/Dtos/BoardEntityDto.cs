namespace Quartz.Infrastructure.Dtos;

public abstract record BoardEntityDto
{
    // Здесь можно оставить пустое тело или вынести общее свойство Id, если оно есть у всех элементов
    public long Line { get; set; }
    public long Column { get; set; }
    public long Length { get; set; }
}