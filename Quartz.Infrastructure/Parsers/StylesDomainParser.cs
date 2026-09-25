using Quartz.Core.Enums;
using Quartz.Core.Interfaces;
using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities.Styles;
using Quartz.Infrastructure.Dtos.Styles;
using Quartz.Infrastructure.Tools;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.NodeDeserializers;

namespace Quartz.Infrastructure.Parsers;

public class StylesDomainParser : IStylesDomainParser
{
    private readonly IDeserializer _deserializer;

    public StylesDomainParser()
    {
        var builder = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .WithNodeDeserializer(
                inner => new PositionNodeDeserializer(inner),
                s => s.InsteadOf<ObjectNodeDeserializer>());

        // Подключаем только карту тегов для стилей
        foreach (var tagMapping in YamlTagRegistry.StylesTagsMap)
        {
            builder.WithTagMapping(tagMapping.Key, tagMapping.Value);
        }

        _deserializer = builder.Build();
    }

    public List<Style> ParseStyles(string yamlText, out List<EditorError> errors)
    {
        // 1. Создаем локальную переменную
        var allErrors = new List<EditorError>();
        // 2. Инициализируем out-параметр ссылкой на наш список
        errors = allErrors;

        if (string.IsNullOrWhiteSpace(yamlText))
            return [];

        try
        {
            // Парсим сразу как список DTO
            var dtos = _deserializer.Deserialize<List<StyleDto>?>(yamlText);
            if (dtos == null || dtos.Count == 0) return [];

            var stylesMap = new Dictionary<string, Style>(StringComparer.OrdinalIgnoreCase);
            var resultList = new List<Style>(dtos.Count);

            var level1Shapes = new List<ShapeStyleDto>();
            var level2SubStyles = new List<StyleDto>();
            var level3MainStyles = new List<StyleDto>();

            // Распределяем по уровням зависимости
            foreach (var styleDto in dtos)
            {
                switch (styleDto)
                {
                    case ShapeStyleDto shape:
                        level1Shapes.Add(shape);
                        break;
                    case PinStyleDto or PadStyleDto:
                        level2SubStyles.Add(styleDto);
                        break;
                    default:
                        level3MainStyles.Add(styleDto);
                        break;
                }
            }

            var defaultUnit = LengthUnit.Mm;

            void ProcessDto(StyleDto styleDto, Style? domainStyle, List<EditorError> localErrors)
            {
                // 3. Захватываем allErrors вместо errors!
                allErrors.AddRange(localErrors);
                if (domainStyle != null)
                {
                    if (!string.IsNullOrWhiteSpace(domainStyle.Name))
                    {
                        if (!stylesMap.TryAdd(domainStyle.Name, domainStyle))
                        {
                            allErrors.Add(new EditorError
                            {
                                Message = $"Дубликат Name стиля: {domainStyle.Name}",
                                Line = styleDto.Line,
                                Column = styleDto.Column,
                                Length = styleDto.Length
                            });
                        }
                    }

                    resultList.Add(domainStyle);
                }
            }

            foreach (var shapeDto in level1Shapes)
            {
                var domain = shapeDto.ToDomain(defaultUnit, out var e);
                ProcessDto(shapeDto, domain, e);
            }

            foreach (var styleDto in level2SubStyles)
            {
                var domain = styleDto.ToDomain(defaultUnit, stylesMap, out var e);
                ProcessDto(styleDto, domain, e);
            }

            foreach (var styleDto in level3MainStyles)
            {
                var domain = styleDto.ToDomain(defaultUnit, stylesMap, out var e);
                ProcessDto(styleDto, domain, e);
            }

            return resultList;
        }
        catch (YamlException ex)
        {
            // 4. Тут тоже используем allErrors
            allErrors.Add(new EditorError
            {
                Message = $"Ошибка десериализации YAML: {ex.InnerException?.Message ?? ex.Message}",
                Line = Math.Max(1, (int)ex.Start.Line),
                Column = Math.Max(1, (int)ex.Start.Column),
                Length = 1
            });

            return [];
        }
    }
}