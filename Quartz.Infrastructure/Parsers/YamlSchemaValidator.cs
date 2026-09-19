// using System.Reflection;
// using Quartz.Core.Interfaces;
// using Quartz.Core.Models;
// using Quartz.Infrastructure.Dtos;
// using YamlDotNet.RepresentationModel;
// using YamlDotNet.Serialization;

// namespace Quartz.Infrastructure.Parsers;

// public class YamlSchemaValidator : IYamlSchemaValidator
// {
//     private static readonly Dictionary<Type, HashSet<string>> PropertyCache = new();

//     // Автоматический маппинг доменных типов в DTO прямо внутри Infrastructure!
//     // Теперь Application-слой не знает про DTO!
//     private static readonly Dictionary<Type, Type> DomainToDtoMap = new()
//     {
//         { typeof(LayerModel), typeof(LayerModelDto) },
//         { typeof(BoardModel), typeof(BoardModelDto) } 
//     };

//     public List<EditorError> ValidateSchemaAndTags(string yamlText, Type? targetType = null)
//     {
//         targetType ??= typeof(LayerModelDto);

//         // Если из Application пришел доменный тип — подменяем его на соответствующий DTO
//         if (targetType != null && DomainToDtoMap.TryGetValue(targetType, out var dtoType))
//         {
//             targetType = dtoType;
//         }

//         var errors = new List<EditorError>();
//         var yaml = new YamlStream();

//         try
//         {
//             using var reader = new StringReader(yamlText);
//             yaml.Load(reader);
//         }
//         catch
//         {
//             return errors;
//         }

//         if (yaml.Documents.Count == 0) return errors;

//         if (yaml.Documents[0].RootNode is YamlMappingNode rootMapping)
//         {
//             ValidateNodeAgainstType(rootMapping, targetType, errors);
//         }

//         return errors;
//     }

//     private static void ValidateNodeAgainstType(YamlMappingNode mapping, Type targetType, List<EditorError> errors)
//     {
//         var allowedProperties = GetAllowedProperties(targetType);

//         foreach (var entry in mapping.Children)
//         {
//             if (entry.Key is not YamlScalarNode keyNode) continue;

//             var keyName = keyNode.Value ?? string.Empty;

//             if (!allowedProperties.Contains(keyName))
//             {
//                 errors.Add(new EditorError
//                 {
//                     Message = $"Свойство '{keyName}' не существует в {targetType.Name}",
//                     Line = keyNode.Start.Line,
//                     Column = keyNode.Start.Column,
//                     Length = keyName.Length
//                 });
//                 continue;
//             }

//             var propertyType = GetPropertyType(targetType, keyName);
//             if (propertyType != null)
//             {
//                 InspectChildNode(entry.Value, propertyType, errors);
//             }
//         }
//     }

//     private static void InspectChildNode(YamlNode node, Type expectedType, List<EditorError> errors)
//     {
//         // 1. Проверка на Dictionary<string, T>
//         if (IsDictionaryType(expectedType, out var valueType))
//         {
//             if (node is YamlMappingNode dictMapping)
//             {
//                 foreach (var entry in dictMapping.Children)
//                 {
//                     if (valueType != null)
//                     {
//                         InspectChildNode(entry.Value, valueType, errors);
//                     }
//                 }
//             }

//             return;
//         }

//         // 2. Проверка одиночного маппинга
//         if (node is YamlMappingNode childMapping)
//         {
//             var targetType = expectedType;

//             if (!node.Tag.IsEmpty)
//             {
//                 var tag = node.Tag.Value;

//                 if (YamlTagRegistry.TagToTypeMap.TryGetValue(tag, out var mappedType))
//                 {
//                     targetType = mappedType;
//                 }
//                 else
//                 {
//                     errors.Add(new EditorError
//                     {
//                         Message = $"Неизвестный тег элемента: '{tag}'",
//                         Line = node.Start.Line,
//                         Column = node.Start.Column,
//                         Length = tag.Length
//                     });
//                     return;
//                 }
//             }

//             ValidateNodeAgainstType(childMapping, targetType, errors);
//         }
//         // 3. Проверка списков (Sequence)
//         else if (node is YamlSequenceNode sequence)
//         {
//             var elementType = GetSequenceElementType(expectedType);

//             foreach (var item in sequence.Children)
//             {
//                 var targetType = elementType;

//                 if (!item.Tag.IsEmpty)
//                 {
//                     var tag = item.Tag.Value;

//                     if (YamlTagRegistry.TagToTypeMap.TryGetValue(tag, out var mappedType))
//                     {
//                         targetType = mappedType;
//                     }
//                     else
//                     {
//                         errors.Add(new EditorError
//                         {
//                             Message = $"Неизвестный тег элемента: '{tag}'",
//                             Line = item.Start.Line,
//                             Column = item.Start.Column,
//                             Length = tag.Length
//                         });
//                         continue;
//                     }
//                 }

//                 if (item is YamlMappingNode itemMapping && targetType != null)
//                 {
//                     ValidateNodeAgainstType(itemMapping, targetType, errors);
//                 }
//                 else if (item is YamlScalarNode && targetType != null)
//                 {
//                     if (IsSimpleType(targetType))
//                     {
//                         continue;
//                     }
//                 }
//             }
//         }
//         // 4. Проверка на скаляр
//         else if (node is YamlScalarNode scalarNode)
//         {
//             if (!scalarNode.Tag.IsEmpty && scalarNode.Tag.Value == "!via")
//             {
//                 return;
//             }

//             if (expectedType == typeof(PadEndpointDto))
//             {
//                 return;
//             }

//             // ВОТ ТУТ ИСПРАВЛЕНИЕ! Теперь мы проверяем простые типы с учетом Nullable<T>!
//             if (IsSimpleType(expectedType))
//             {
//                 return;
//             }

//             errors.Add(new EditorError
//             {
//                 Message =
//                     $"Ожидался объект типа '{expectedType.Name}', но получено простое значение '{scalarNode.Value}'",
//                 Line = scalarNode.Start.Line,
//                 Column = scalarNode.Start.Column,
//                 Length = scalarNode.Value?.Length ?? 1
//             });
//         }
//     }

//     // Вспомогательный метод для проверки простых типов (включая Nullable<double>, Nullable<int> и т.д.)
//     private static bool IsSimpleType(Type type)
//     {
//         var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
//         return underlyingType.IsPrimitive || underlyingType.IsEnum || underlyingType == typeof(string) ||
//                underlyingType == typeof(decimal);
//     }

//     private static bool IsDictionaryType(Type type, out Type? valueType)
//     {
//         valueType = null;
//         if (type.IsGenericType)
//         {
//             var genDef = type.GetGenericTypeDefinition();
//             if (genDef == typeof(Dictionary<,>) ||
//                 genDef == typeof(IReadOnlyDictionary<,>) ||
//                 genDef == typeof(IDictionary<,>))
//             {
//                 valueType = type.GetGenericArguments()[1];
//                 return true;
//             }
//         }

//         return false;
//     }

//     private static HashSet<string> GetAllowedProperties(Type type)
//     {
//         if (PropertyCache.TryGetValue(type, out var cached)) return cached;

//         var properties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

//         foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
//         {
//             var yamlAttr = prop.GetCustomAttribute<YamlMemberAttribute>();
//             var name = yamlAttr?.Alias ?? prop.Name;
//             properties.Add(name);
//         }

//         PropertyCache[type] = properties;
//         return properties;
//     }

//     private static Type? GetPropertyType(Type parentType, string propertyName)
//     {
//         var prop = parentType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
//             .FirstOrDefault(p => string.Equals(p.GetCustomAttribute<YamlMemberAttribute>()?.Alias ?? p.Name,
//                 propertyName, StringComparison.OrdinalIgnoreCase));

//         return prop?.PropertyType;
//     }

//     private static Type? GetSequenceElementType(Type sequenceType)
//     {
//         if (sequenceType.IsGenericType && sequenceType.GetGenericTypeDefinition() == typeof(List<>))
//         {
//             return sequenceType.GetGenericArguments()[0];
//         }

//         return sequenceType.GetElementType();
//     }
// }


using System.Reflection;
using Quartz.Core.Interfaces;
using Quartz.Core.Models;
using Quartz.Infrastructure.Dtos;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Parsers;

public class YamlSchemaValidator : IYamlSchemaValidator
{
    private static readonly Dictionary<Type, HashSet<string>> PropertyCache = new();

    private static readonly Dictionary<Type, Type> DomainToDtoMap = new()
    {
        { typeof(LayerModel), typeof(LayerModelDto) },
        { typeof(BoardModel), typeof(BoardModelDto) }
    };

    public List<EditorError> ValidateSchemaAndTags(string yamlText, Type? targetType = null)
    {
        targetType ??= typeof(LayerModelDto);

        if (targetType != null && DomainToDtoMap.TryGetValue(targetType, out var dtoType))
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
                InspectChildNode(entry.Value, propertyType, errors, keyName);
            }
        }
    }

    private static void InspectChildNode(YamlNode node, Type expectedType, List<EditorError> errors,
        string propertyName = "")
    {
        // 1. Проверка на Dictionary<string, T>
        if (IsDictionaryType(expectedType, out var valueType))
        {
            if (node is YamlMappingNode dictMapping)
            {
                foreach (var entry in dictMapping.Children)
                {
                    if (valueType != null)
                    {
                        InspectChildNode(entry.Value, valueType, errors, propertyName);
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
                else if (item is YamlScalarNode scalarItem && targetType != null)
                {
                    if (IsSimpleType(targetType))
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
                    else
                    {
                        // ВОТ ЭТОЙ ВЕТКИ НЕ ХВАТАЛО!
                        // Элемент списка — скаляр (например, lol), но типом элемента является сложный объект (PinDto)
                        errors.Add(new EditorError
                        {
                            Message =
                                $"Элемент списка ожидает объект типа '{GetFriendlyTypeName(targetType)}', но получено простое значение '{scalarItem.Value}'",
                            Line = scalarItem.Start.Line,
                            Column = scalarItem.Start.Column,
                            Length = string.IsNullOrEmpty(scalarItem.Value) ? 1 : scalarItem.Value.Length
                        });
                    }
                }
            }
        }
        // 4. Проверка на скаляр
        else if (node is YamlScalarNode scalarNode)
        {
            if (!scalarNode.Tag.IsEmpty && scalarNode.Tag.Value == "!via")
            {
                return;
            }

            if (expectedType == typeof(PadEndpointDto))
            {
                return;
            }

            if (IsSimpleType(expectedType))
            {
                var underlyingType = Nullable.GetUnderlyingType(expectedType) ?? expectedType;

                if (underlyingType != typeof(string) && !IsValidScalarValue(scalarNode, underlyingType))
                {
                    var propInfo = string.IsNullOrEmpty(propertyName) ? "" : $" для свойства '{propertyName}'";
                    var valInfo = string.IsNullOrEmpty(scalarNode.Value) ? "пустая строка" : $"'{scalarNode.Value}'";

                    errors.Add(new EditorError
                    {
                        Message = $"Ожидалось значение типа '{underlyingType.Name}'{propInfo}, но получено {valInfo}",
                        Line = scalarNode.Start.Line,
                        Column = scalarNode.Start.Column,
                        Length = string.IsNullOrEmpty(scalarNode.Value) ? 1 : scalarNode.Value.Length
                    });
                }

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

    private static bool IsValidScalarValue(YamlScalarNode scalarNode, Type targetType)
    {
        var value = scalarNode.Value;

        // Неэкранированное пустое значение в plain-стиле означает null в YAML (допустимо для Nullable)
        if (string.IsNullOrWhiteSpace(value) || value == "null" || value == "~")
        {
            if (scalarNode.Style == ScalarStyle.Plain)
            {
                return true;
            }

            return false; // Это явная пустая строка "", что не является числом!
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