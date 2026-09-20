using Quartz.Infrastructure.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Parsers;

public sealed class PositionNodeDeserializer : INodeDeserializer
{
    private readonly INodeDeserializer _inner;

    public PositionNodeDeserializer(INodeDeserializer inner)
    {
        _inner = inner;
    }

    public bool Deserialize(
        IParser reader,
        Type expectedType,
        Func<IParser, Type, object?> nestedObjectDeserializer,
        out object? value,
        ObjectDeserializer rootDeserializer)
    {
        value = null;
        Mark? start = reader.Current?.Start;

        try
        {
            if (_inner.Deserialize(reader, expectedType, nestedObjectDeserializer, out value, rootDeserializer))
            {
                SetPosition(value, start, reader.Current?.Start);
                return true;
            }
        }
        catch
        {
            // При неполном YAML во время редактирования YamlDotNet может бросить исключение.
            // Возвращаем true, чтобы парсер не падал в истерику:
            value = expectedType.IsValueType ? Activator.CreateInstance(expectedType) : null;
            return true;
        }

        return false;
    }

    private static void SetPosition(object? value, Mark? start, Mark? end = null)
    {
        if (value is not BoardEntityDto entity || !start.HasValue)
            return;

        entity.Line = Math.Max(1, (int)start.Value.Line);
        entity.Column = Math.Max(1, (int)start.Value.Column);

        if (end.HasValue && end.Value.Index >= start.Value.Index)
        {
            entity.Length = Math.Max(1, (int)(end.Value.Index - start.Value.Index));
        }
        else
        {
            entity.Length = 1;
        }
    }
}