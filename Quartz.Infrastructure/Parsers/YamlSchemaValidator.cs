using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Quartz.Core.Interfaces;
using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities.Styles;
using Quartz.Infrastructure.Dtos;
using Quartz.Infrastructure.Dtos.Styles;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Parsers;

public class YamlSchemaValidator : IYamlSchemaValidator
{
    private static readonly Dictionary<Type, HashSet<string>> PropertyCache = new();
    private static readonly Dictionary<Type, HashSet<string>> RequiredPropertyCache = new();

    private static readonly Dictionary<Type, Type> DomainToDtoMap = new()
    {
        { typeof(LayerModel), typeof(LayerModelDto) },
        { typeof(BoardModel), typeof(BoardModelDto) },
        { typeof(List<Style>), typeof(List<StyleDto>) }
    };

    public List<EditorError> ValidateSchemaAndTags(string yamlText, Type? targetType = null)
    {
        targetType ??= typeof(LayerModelDto);

        if (DomainToDtoMap.TryGetValue(targetType, out var dtoType))
        {
            targetType = dtoType;
        }

        var errors = new List<EditorError>();
        var yaml = new YamlStream();

        try
        {
            using var reader = new StringReader(yamlText);
            yaml.Load(reader);
        }
        catch (YamlException ex)
        {
            errors.Add(new EditorError
            {
                Message = $"Ошибка синтаксиса YAML: {ex.Message}",
                Line = ex.Start.Line,
                Column = ex.Start.Column,
                Length = 1
            });
            return errors;
        }
        catch
        {
            return errors;
        }

        if (yaml.Documents.Count == 0) return errors;

        // Определяем карту тегов в зависимости от валидируемой модели
        var tagsMap = targetType switch
        {
            _ when targetType == typeof(BoardModelDto) => YamlTagRegistry.BoardTagsMap,
            _ when targetType == typeof(LayerModelDto) => YamlTagRegistry.LayerTagsMap,
            _ when targetType == typeof(List<StyleDto>) => YamlTagRegistry.StylesTagsMap,
            _ => throw new ArgumentException("Неподдерживаемый тип")
        };


        var rootNode = yaml.Documents[0].RootNode;

        if (rootNode is YamlMappingNode rootMapping)
        {
            ValidateNodeAgainstType(rootMapping, targetType, errors, tagsMap);
        }
        else if (rootNode is YamlSequenceNode rootSequence)
        {
            var elementType = GetSequenceElementType(targetType);
            if (elementType == null)
            {
                errors.Add(new EditorError
                {
                    Message = $"Ожидался объект '{GetFriendlyTypeName(targetType)}', но передан список",
                    Line = rootSequence.Start.Line,
                    Column = rootSequence.Start.Column,
                    Length = 1
                });
                return errors;
            }

            InspectChildNode(rootSequence, targetType, errors, tagsMap, "RootList");
        }

        return errors;
    }

    private static void ValidateNodeAgainstType(
        YamlMappingNode mapping,
        Type targetType,
        List<EditorError> errors,
        IReadOnlyDictionary<string, Type> tagsMap)
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
                    Message = $"Свойство '{keyName}' не существует в {GetFriendlyTypeName(targetType)}",
                    Line = keyNode.Start.Line,
                    Column = keyNode.Start.Column,
                    Length = keyName.Length
                });
                continue;
            }

            var propertyType = GetPropertyType(targetType, keyName);
            if (propertyType != null)
            {
                InspectChildNode(entry.Value, propertyType, errors, tagsMap, keyName);
            }
        }

        var requiredProperties = GetRequiredProperties(targetType);

        foreach (var reqProp in requiredProperties)
        {
            var pair = mapping.Children.FirstOrDefault(c =>
                c.Key is YamlScalarNode keyNode &&
                string.Equals(keyNode.Value, reqProp, StringComparison.OrdinalIgnoreCase));

            bool isMissingOrEmpty = pair.Key == null ||
                                    (pair.Value is YamlScalarNode valueNode &&
                                     string.IsNullOrWhiteSpace(valueNode.Value));

            if (isMissingOrEmpty)
            {
                int length = 1;
                long line = mapping.Start.Line;
                long column = mapping.Start.Column;

                if (pair.Key is YamlScalarNode keyNode)
                {
                    line = keyNode.Start.Line;
                    column = keyNode.Start.Column;
                    length = keyNode.Value?.Length ?? 1;
                }
                else if (!mapping.Tag.IsEmpty)
                {
                    length = mapping.Tag.Value.Length;
                }

                errors.Add(new EditorError
                {
                    Message =
                        $"Обязательное свойство '{reqProp}' не заполнено в объекте '{GetFriendlyTypeName(targetType)}'",
                    Line = line,
                    Column = column,
                    Length = length
                });
            }
        }
    }

    private static void InspectChildNode(
        YamlNode node,
        Type expectedType,
        List<EditorError> errors,
        IReadOnlyDictionary<string, Type> tagsMap,
        string propertyName = "")
    {
        // 1. ПРОВЕРКА НЕИЗВЕСТНЫХ ТЕГОВ
        if (!node.Tag.IsEmpty)
        {
            var tag = node.Tag.Value;
            if (!tagsMap.ContainsKey(tag))
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

        // 2. ЕСЛИ ОЖИДАЕТСЯ ПРОСТОЙ ТИП
        if (IsSimpleType(expectedType))
        {
            if (node is not YamlScalarNode scalarNode)
            {
                var nodeType = node is YamlMappingNode ? "объект" : "список";
                var propInfo = string.IsNullOrEmpty(propertyName) ? "" : $" для свойства '{propertyName}'";

                errors.Add(new EditorError
                {
                    Message =
                        $"Ожидалось значение типа '{GetFriendlyTypeName(expectedType)}'{propInfo}, но передан {nodeType}",
                    Line = node.Start.Line,
                    Column = node.Start.Column,
                    Length = 1
                });
                return;
            }

            if (!scalarNode.Tag.IsEmpty && scalarNode.Tag.Value == "!via") return;
            if (expectedType == typeof(PadEndpointDto)) return;

            var underlyingType = Nullable.GetUnderlyingType(expectedType) ?? expectedType;
            if (underlyingType != typeof(string) && !IsValidScalarValue(scalarNode, underlyingType))
            {
                var propInfo = string.IsNullOrEmpty(propertyName) ? "" : $" для свойства '{propertyName}'";
                var valInfo = string.IsNullOrEmpty(scalarNode.Value) ? "пустая строка" : $"'{scalarNode.Value}'";
                errors.Add(new EditorError
                {
                    Message =
                        $"Ожидалось значение типа '{GetFriendlyTypeName(underlyingType)}'{propInfo}, но получено {valInfo}",
                    Line = scalarNode.Start.Line,
                    Column = scalarNode.Start.Column,
                    Length = string.IsNullOrEmpty(scalarNode.Value) ? 1 : scalarNode.Value.Length
                });
            }

            return;
        }

        // 3. ЕСЛИ ОЖИДАЕТСЯ СЛОВАРЬ (Dictionary)
        if (IsDictionaryType(expectedType, out var valueType))
        {
            if (node is YamlMappingNode dictMapping)
            {
                foreach (var entry in dictMapping.Children)
                {
                    if (valueType != null) InspectChildNode(entry.Value, valueType, errors, tagsMap, propertyName);
                }
            }

            return;
        }

        // 4. ЕСЛИ ОЖИДАЕТСЯ СЛОЖНЫЙ ОБЪЕКТ / СПИСОК
        if (node is YamlMappingNode childMapping)
        {
            var targetType = expectedType;
            if (!node.Tag.IsEmpty && tagsMap.TryGetValue(node.Tag.Value, out var mappedType))
            {
                targetType = mappedType;
            }

            ValidateNodeAgainstType(childMapping, targetType, errors, tagsMap);
        }
        else if (node is YamlSequenceNode sequence)
        {
            var elementType = GetSequenceElementType(expectedType);

            if (elementType == null)
            {
                errors.Add(new EditorError
                {
                    Message = $"Ожидался объект '{GetFriendlyTypeName(expectedType)}', но передан список",
                    Line = sequence.Start.Line,
                    Column = sequence.Start.Column,
                    Length = 1
                });
                return;
            }

            foreach (var item in sequence.Children)
            {
                var targetType = elementType;
                if (!item.Tag.IsEmpty)
                {
                    var tag = item.Tag.Value;
                    if (tagsMap.TryGetValue(tag, out var mappedType))
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

                if (item is YamlMappingNode itemMapping)
                {
                    ValidateNodeAgainstType(itemMapping, targetType, errors, tagsMap);
                }
                else if (item is YamlScalarNode scalarItem)
                {
                    if (!IsSimpleType(targetType))
                    {
                        ValidateScalarAsObject(scalarItem, targetType, errors);
                    }
                    else
                    {
                        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                        if (underlyingType != typeof(string) && !IsValidScalarValue(scalarItem, underlyingType))
                        {
                            errors.Add(new EditorError
                            {
                                Message =
                                    $"Элемент списка ожидает тип '{GetFriendlyTypeName(underlyingType)}', но получено значение '{scalarItem.Value}'",
                                Line = scalarItem.Start.Line,
                                Column = scalarItem.Start.Column,
                                Length = string.IsNullOrEmpty(scalarItem.Value) ? 1 : scalarItem.Value.Length
                            });
                        }
                    }
                }
            }
        }
        else if (node is YamlScalarNode scalarNode)
        {
            var targetType = expectedType;
            if (!scalarNode.Tag.IsEmpty && tagsMap.TryGetValue(scalarNode.Tag.Value, out var mappedType))
            {
                targetType = mappedType;
            }

            if (!IsSimpleType(targetType))
            {
                if (targetType == typeof(ViaEndpointDto))
                    return;

                ValidateScalarAsObject(scalarNode, targetType, errors);
            }
        }
    }

    private static void ValidateScalarAsObject(YamlScalarNode scalarNode, Type targetType, List<EditorError> errors)
    {
        if (!string.IsNullOrWhiteSpace(scalarNode.Value))
        {
            errors.Add(new EditorError
            {
                Message =
                    $"Ожидались свойства объекта '{GetFriendlyTypeName(targetType)}', а не одиночное значение '{scalarNode.Value}'",
                Line = scalarNode.Start.Line,
                Column = scalarNode.Start.Column,
                Length = scalarNode.Value.Length
            });
            return;
        }

        var requiredProperties = GetRequiredProperties(targetType);
        foreach (var reqProp in requiredProperties)
        {
            errors.Add(new EditorError
            {
                Message =
                    $"Обязательное свойство '{reqProp}' не заполнено в объекте '{GetFriendlyTypeName(targetType)}'",
                Line = scalarNode.Start.Line,
                Column = scalarNode.Start.Column,
                Length = !scalarNode.Tag.IsEmpty ? scalarNode.Tag.Value.Length : 1
            });
        }
    }

    private static HashSet<string> GetRequiredProperties(Type type)
    {
        if (RequiredPropertyCache.TryGetValue(type, out var cached)) return cached;

        var properties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetCustomAttribute<RequiredAttribute>() != null)
            {
                var yamlAttr = prop.GetCustomAttribute<YamlMemberAttribute>();
                var name = yamlAttr?.Alias ?? prop.Name;
                properties.Add(name);
            }
        }

        RequiredPropertyCache[type] = properties;
        return properties;
    }

    private static bool IsValidScalarValue(YamlScalarNode scalarNode, Type targetType)
    {
        var value = scalarNode.Value;

        if (string.IsNullOrWhiteSpace(value) || value == "null" || value == "~")
        {
            return scalarNode.Style == ScalarStyle.Plain;
        }

        if (targetType == typeof(double) || targetType == typeof(float) || targetType == typeof(decimal))
        {
            return double.TryParse(value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out _);
        }

        if (targetType == typeof(int) || targetType == typeof(long) || targetType == typeof(short) ||
            targetType == typeof(byte))
        {
            return long.TryParse(value, out _);
        }

        if (targetType == typeof(bool))
        {
            return bool.TryParse(value, out _);
        }

        if (targetType.IsEnum)
        {
            return Enum.IsDefined(targetType, value) || Enum.TryParse(targetType, value, true, out _);
        }

        return true;
    }

    private static bool IsSimpleType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        return underlyingType.IsPrimitive || underlyingType.IsEnum || underlyingType == typeof(string) ||
               underlyingType == typeof(decimal);
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

    private static string GetFriendlyTypeName(Type type)
    {
        if (type == typeof(string)) return "строка";
        if (type == typeof(int) || type == typeof(double) || type == typeof(float)) return "число";
        if (type == typeof(bool)) return "булево значение";

        if (type.IsGenericType)
        {
            var cleanName = type.Name[..type.Name.IndexOf('`')];
            var args = string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName));

            if (cleanName is "List" or "IList" or "IEnumerable")
            {
                return $"список '{args}'";
            }

            return $"{cleanName}<{args}>";
        }

        return type.Name;
    }
}