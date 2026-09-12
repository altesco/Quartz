namespace Quartz.Infrastructure.Dtos;

public abstract record BoardEntityDto
{   
    public long Line { get; set; }
    public long Column { get; set; }
    public long Length { get; set; }
}