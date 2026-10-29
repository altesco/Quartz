namespace Quartz.Core.Models;

public class EditorError : Problem
{
    public EditorError() { }

    public EditorError(string message, long line = 0, long column = 0, long length = 0)
    {
        Message = message;
        Line = line;
        Column = column;
        Length = length;
    }
}