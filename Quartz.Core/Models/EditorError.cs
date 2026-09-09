namespace Quartz.Core.Models;

public class EditorError
{
    public string Message { get; set; } = string.Empty;
    public long Line { get; set; }
    public long Column { get; set; }
    public long Length { get; set; }
}