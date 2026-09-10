using Quartz.Core.Interfaces;
using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities;
using Quartz.Infrastructure.Dtos;
using Quartz.Infrastructure.Tools;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.NodeDeserializers; 

namespace Quartz.Infrastructure.Parsers;

public class YamlDomainParser : IYamlDomainParser
{
    private readonly IDeserializer _deserializer;

    public YamlDomainParser()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .IgnoreUnmatchedProperties()

            // Оборачиваем парсер для получения позиций
            .WithNodeDeserializer(
                inner => new PositionNodeDeserializer(inner), 
                s => s.InsteadOf<ObjectNodeDeserializer>())
            
            .WithTagMapping("!rect", typeof(RectShapeDto))
            .WithTagMapping("!path", typeof(PathShapeDto))
            
            .WithTagMapping("!resistor", typeof(ResistorDto))
            .WithTagMapping("!capacitor", typeof(CapacitorDto))
            .WithTagMapping("!transistor", typeof(TransistorDto))
            .WithTagMapping("!diode", typeof(DiodeDto))
            .WithTagMapping("!inductor", typeof(InductorDto))
            .WithTagMapping("!ic", typeof(IntegratedCircuitDto))
            .WithTagMapping("!connector", typeof(ConnectorDto))
            .WithTagMapping("!trace", typeof(TraceDto))
            .WithTagMapping("!line", typeof(LineSegmentDto))
            .WithTagMapping("!arc", typeof(ArcSegmentDto))
            .Build();
    }

    public LayerModel? Parse(string yamlText, out List<EditorError> errors)
    {
        // Обязательная инициализация out-параметра в самом начале
        errors = [];

        if (string.IsNullOrWhiteSpace(yamlText))
        {
            return new LayerModel
            {
                Shape = new RectShape
                {
                    Width = 600,
                    Height = 800
                }
            };
        }

        try
        {
            var dto = _deserializer.Deserialize<LayerModelDto?>(yamlText);
            if (dto == null)
            {
                errors.Add(new EditorError { Message = "Файл пуст или имеет неверный формат" });
                return null;
            }

            // Твой экстеншен ToDomain уже возвращает ошибки через out,
            // поэтому мы просто передаем туда наш массив и возвращаем саму модель
            return dto.ToDomain(out errors);
        }
        catch (YamlException ex)
        {
            errors.Add(new EditorError 
            { 
                Message = $"Синтаксическая ошибка YAML: {ex.InnerException?.Message ?? ex.Message}",
                Line = (int)ex.Start.Line,
                Column = (int)ex.Start.Column,
                Length = 1 
            });
            return null;
        }
        catch (Exception ex)
        {
            errors.Add(new EditorError { Message = $"Критическая ошибка парсера: {ex.Message}" });
            return null;
        }
    }

    private sealed class PositionNodeDeserializer : INodeDeserializer
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
            var start = reader.Current?.Start;
            value = null;

            try
            {
                if (_inner.Deserialize(reader, expectedType, nestedObjectDeserializer, out value, rootDeserializer))
                {
                    if (value is BoardEntityDto element && start.HasValue)
                    {
                        element.Line = (int)start.Value.Line;
                        element.Column = (int)start.Value.Column;

                        var end = reader.Current?.End;
                        if (end.HasValue && end.Value.Index >= start.Value.Index)
                        {
                            element.Length = (int)(end.Value.Index - start.Value.Index);
                        }
                    }
                    return true;
                }
            }
            catch
            {
                // Глотаем исключение при неполном вводе во время печати,
                // чтобы приложение не падало на каждой недописанной букве.
            }

            return false;
        }
    }
}