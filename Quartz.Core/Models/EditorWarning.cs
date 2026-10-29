namespace Quartz.Core.Models;

public class EditorWarning : Problem
{
    public EditorWarning() { }

    public EditorWarning(string message, long line = 0, long column = 0, long length = 0)
    {
        Message = message;
        Line = line;
        Column = column;
        Length = length;
    }
}
