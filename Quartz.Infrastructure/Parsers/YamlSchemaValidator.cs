using System.Reflection;
using Quartz.Core.Interfaces;
using Quartz.Core.Models;
using Quartz.Infrastructure.Dtos;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Parsers;

public class YamlSchemaValidator : IYamlSchemaValidator
{
    // Кеш свойств C#-типов, чтобы не дёргать рефлексию на каждый символ
    private static readonly Dictionary<Type, HashSet<string>> PropertyCache = new();

    public List<EditorError> ValidateSchemaAndTags(string yamlText, Type? targetType = null)
    {
        targetType ??= typeof(LayerModel); // Если тип не передан, считаем слоем
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
            ValidateNodeAgainstType(rootMapping, targetType, errors);
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

            var propertyType = GetPropertyType(targetType, keyName);
            if (propertyType != null)
            {
                InspectChildNode(entry.Value, propertyType, errors);
            }
        }
    }

    private static void InspectChildNode(YamlNode node, Type expectedType, List<EditorError> errors)
    {
        // 1. Проверка на Dictionary<string, T> (для components, nets, layers, vias)
        if (IsDictionaryType(expectedType, out var valueType))
        {
            if (node is YamlMappingNode dictMapping)
            {
                foreach (var entry in dictMapping.Children)
                {
                    // В словаре ключи — это ID объектов (например, "R1"), а значения — сами объекты
                    if (valueType != null)
                    {
                        InspectChildNode(entry.Value, valueType, errors);
                    }
                }
            }

            return;
        }

        // 2. Проверка одиночного маппинга
        if (node is YamlMappingNode childMapping)
        {
            var targetType = expectedType;

            if (!node.Tag.IsEmpty)
            {
                var tag = node.Tag.Value;

                if (YamlTagRegistry.TagToTypeMap.TryGetValue(tag, out var mappedType))
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
        // 3. Проверка списков (Sequence)
        else if (node is YamlSequenceNode sequence)
        {
            var elementType = GetSequenceElementType(expectedType);

            foreach (var item in sequence.Children)
            {
                var targetType = elementType;

                if (!item.Tag.IsEmpty)
                {
                    var tag = item.Tag.Value;

                    if (YamlTagRegistry.TagToTypeMap.TryGetValue(tag, out var mappedType))
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
                else if (item is YamlScalarNode && targetType != null)
                {
                    // Пропускаем скаляры в списках (например, список строк для чего-нибудь)
                    if (targetType == typeof(string) || targetType.IsPrimitive || targetType.IsEnum)
                    {
                        continue;
                    }
                }
            }
        }
        // 4. Проверка на скаляр (Вариант БЕЗ тегов для Via и базовых типов)
        else if (node is YamlScalarNode scalarNode)
        {
            // Вот сюда вставляешь! И обязательно проверяешь IsEmpty!
            if (!scalarNode.Tag.IsEmpty && scalarNode.Tag.Value == "!via")
            {
                return;
            }

            if (expectedType == typeof(PadEndpointDto))
            {
                // Это просто строка (имя via), валидатор пропускает, так как схема верна
                return;
            }

            // Также пропускаем обычные свойства-примитивы, чтобы валидатор не ругался на числа и строки
            if (expectedType == typeof(string) || expectedType.IsPrimitive || expectedType.IsEnum)
            {
                return;
            }

            errors.Add(new EditorError
            {
                Message =
                    $"Ожидался объект типа '{expectedType.Name}', но получено простое значение '{scalarNode.Value}'",
                Line = scalarNode.Start.Line,
                Column = scalarNode.Start.Column,
                Length = scalarNode.Value?.Length ?? 1
            });
        }
    }

    private static bool IsDictionaryType(Type type, out Type? valueType)
    {
        valueType = null;
        if (type.IsGenericType)
        {
            var genDef = type.GetGenericTypeDefinition();
            if (genDef == typeof(Dictionary<,>) ||
                genDef == typeof(IReadOnlyDictionary<,>) ||
                genDef == typeof(IDictionary<,>))
            {
                valueType = type.GetGenericArguments()[1];
                return true;
            }
        }

        return false;
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