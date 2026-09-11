using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Quartz.Core.Interfaces;
using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities;
using Quartz.Infrastructure.Dtos;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Parsers;

public class YamlSchemaValidator : IYamlSchemaValidator
{
    // 1. Реестр соответствия тегов YAML и C#-типов моделей
    private static readonly Dictionary<string, Type> TagToTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "!rect", typeof(RectShapeDto) },
        { "!path", typeof(PathShapeDto) },
        { "!resistor", typeof(ResistorDto) },
        { "!capacitor", typeof(CapacitorDto) },
        { "!transistor", typeof(TransistorDto) },
        { "!diode", typeof(DiodeDto) },
        { "!inductor", typeof(InductorDto) },
        { "!ic", typeof(IntegratedCircuitDto) },
        { "!connector", typeof(ConnectorDto) },
        //{ "!trace", typeof(TraceDto) },
        { "!line", typeof(LineSegmentDto) },
        { "!arc", typeof(ArcSegmentDto) }
    };

    // Кеш свойств C#-типов, чтобы не дёргать рефлексию на каждый символ
    private static readonly Dictionary<Type, HashSet<string>> PropertyCache = new();

    public List<EditorError> ValidateSchemaAndTags(string yamlText)
    {
        var errors = new List<EditorError>();
        var yaml = new YamlStream();

        try
        {
            using var reader = new StringReader(yamlText);
            yaml.Load(reader);
        }
        catch
        {
            return errors;
        }

        if (yaml.Documents.Count == 0) return errors;

        if (yaml.Documents[0].RootNode is YamlMappingNode rootMapping)
        {
            // Валидируем корень относительно модели LayerModel
            ValidateNodeAgainstType(rootMapping, typeof(LayerModel), errors);
        }

        return errors;
    }

    private static void ValidateNodeAgainstType(YamlMappingNode mapping, Type targetType, List<EditorError> errors)
    {
        var allowedProperties = GetAllowedProperties(targetType);

        foreach (var entry in mapping.Children)
        {
            if (entry.Key is not YamlScalarNode keyNode) continue;

            var keyName = keyNode.Value ?? string.Empty;

            // 1. Проверяем, существует ли такое свойство в C#-модели
            if (!allowedProperties.Contains(keyName))
            {
                errors.Add(new EditorError
                {
                    Message = $"Свойство '{keyName}' не существует в {targetType.Name}",
                    Line = keyNode.Start.Line,
                    Column = keyNode.Start.Column,
                    Length = keyName.Length
                });
                continue;
            }

            // 2. Определяем C#-тип вложенного объекта для дальнейшей рекурсивной проверки
            var propertyType = GetPropertyType(targetType, keyName);
            if (propertyType != null)
            {
                InspectChildNode(entry.Value, propertyType, errors);
            }
        }
    }

    private static void InspectChildNode(
    YamlNode node,
    Type expectedType,
    List<EditorError> errors)
    {
        if (node is YamlMappingNode childMapping)
        {
            var targetType = expectedType;

            // Полиморфный YAML-тег:
            // shape: !rect
            //   width: 20
            if (!node.Tag.IsEmpty)
            {
                var tag = node.Tag.Value;

                if (TagToTypeMap.TryGetValue(tag, out var mappedType))
                {
                    targetType = mappedType;
                }
                else
                {
                    errors.Add(new EditorError
                    {
                        Message = $"Неизвестный тег элемента: '{tag}'",
                        Line = node.Start.Line,
                        Column = node.Start.Column,
                        Length = tag.Length
                    });

                    return;
                }
            }

            ValidateNodeAgainstType(childMapping, targetType, errors);
        }
        else if (node is YamlSequenceNode sequence)
        {
            var elementType = GetSequenceElementType(expectedType);

            foreach (var item in sequence.Children)
            {
                var targetType = elementType;

                // Полиморфные элементы коллекции:
                // - !resistor
                // - !capacitor
                if (!item.Tag.IsEmpty)
                {
                    var tag = item.Tag.Value;

                    if (TagToTypeMap.TryGetValue(tag, out var mappedType))
                    {
                        targetType = mappedType;
                    }
                    else
                    {
                        errors.Add(new EditorError
                        {
                            Message = $"Неизвестный тег элемента: '{tag}'",
                            Line = item.Start.Line,
                            Column = item.Start.Column,
                            Length = tag.Length
                        });

                        continue;
                    }
                }

                if (item is YamlMappingNode itemMapping && targetType != null)
                {
                    ValidateNodeAgainstType(itemMapping, targetType, errors);
                }
            }
        }
    }

    // Извлекаем имена всех публичных свойств C#-класса (учитывая [YamlMember(Alias="...")])
    private static HashSet<string> GetAllowedProperties(Type type)
    {
        if (PropertyCache.TryGetValue(type, out var cached)) return cached;

        var properties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var yamlAttr = prop.GetCustomAttribute<YamlMemberAttribute>();
            var name = yamlAttr?.Alias ?? prop.Name;
            properties.Add(name);
        }

        PropertyCache[type] = properties;
        return properties;
    }

    private static Type? GetPropertyType(Type parentType, string propertyName)
    {
        var prop = parentType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => string.Equals(p.GetCustomAttribute<YamlMemberAttribute>()?.Alias ?? p.Name,
                propertyName, StringComparison.OrdinalIgnoreCase));

        return prop?.PropertyType;
    }

    private static Type? GetSequenceElementType(Type sequenceType)
    {
        if (sequenceType.IsGenericType && sequenceType.GetGenericTypeDefinition() == typeof(List<>))
        {
            return sequenceType.GetGenericArguments()[0];
        }

        return sequenceType.GetElementType();
    }
}