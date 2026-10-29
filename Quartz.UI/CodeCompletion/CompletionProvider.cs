using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using Lucide.Avalonia;
using Quartz.Core.Enums;
using Quartz.Infrastructure.Dtos;
using Quartz.Infrastructure.Dtos.Styles;
using Quartz.Infrastructure.Parsers;
using Quartz.UI.ViewModels;
using YamlDotNet.Serialization;
using YamlDotNet.RepresentationModel;

namespace Quartz.UI.CodeCompletion
{
    /// <summary>
    /// Полностью динамический провайдер автодополнения для YAML.
    /// Автоматически извлекает свойства и теги через рефлексию из DTO-классов,
    /// атрибутов [YamlMember], реестра YamlTagRegistry и перечислений Core.
    /// Единый источник правды: при добавлении свойств в DTO на бэкенде они сразу появляются здесь.
    /// </summary>
    public static class CompletionProvider
    {
        private static readonly ConcurrentDictionary<Type, IReadOnlyList<QuartzCompletionData>> PropertyCache = new();
        private static readonly ConcurrentDictionary<Type, IReadOnlyList<QuartzCompletionData>> EnumCache = new();

        private static readonly IReadOnlyList<QuartzCompletionData> BooleanValues = new QuartzCompletionData[]
        {
            new("true", LucideIconKind.Check, "Boolean", "true"),
            new("false", LucideIconKind.X, "Boolean", "false")
        };

        public static IReadOnlyList<QuartzCompletionData> GetCompletions(
            TextEditor editor,
            string currentWord,
            int caretOffset)
        {
            if (editor?.Document == null)
                return Array.Empty<QuartzCompletionData>();

            var vm = editor.DataContext as EditorVM;
            var filePath = vm?.FilePath ?? string.Empty;
            return GetCompletions(editor.Document, filePath, caretOffset, currentWord, editor, vm);
        }

        public static IReadOnlyList<QuartzCompletionData> GetCompletions(
            TextDocument doc,
            string filePath,
            int caretOffset,
            string currentWord = "",
            TextEditor? editor = null,
            EditorVM? vm = null)
        {
            if (doc == null || caretOffset < 0 || caretOffset > doc.TextLength)
                return Array.Empty<QuartzCompletionData>();

            var rootType = DetectRootType(doc, filePath);

            var caretLine = doc.GetLineByOffset(caretOffset);
            var lineText = doc.GetText(caretLine.Offset, caretLine.Length);
            var textBeforeCaret = doc.GetText(caretLine.Offset, caretOffset - caretLine.Offset);

            int currentIndent = GetEffectiveIndent(doc, caretLine.LineNumber);
            bool isListItem = lineText.TrimStart().StartsWith("-");

            // 1. Разрешаем тип текущего объекта и свойство-контейнер
            var (containerType, containerProperty, objectStartLine) =
                ResolveEnclosingObject(doc, caretLine.LineNumber, currentIndent, rootType);

            // 2. Если курсор находится после двоеточия на текущей строке, подсказываем значения свойства
            int colonIndex = textBeforeCaret.IndexOf(':');
            if (colonIndex >= 0)
            {
                var propertyAlias = textBeforeCaret[..colonIndex].Trim().TrimStart('-', ' ');
                return ResolveValueCompletions(editor, filePath, containerType, propertyAlias, rootType, vm);
            }

            // 3. Пользователь набирает ключ свойства или YAML-тег:
            // Исключаем свойства, которые уже прописаны в текущем объекте
            var targetDtoType = containerType ?? rootType;
            var existingKeys = CollectExistingPropertyKeys(doc, caretLine.LineNumber, objectStartLine, currentIndent, targetDtoType);

            return ResolveKeyOrTagCompletions(editor, containerType, containerProperty, isListItem, currentIndent, filePath, rootType, existingKeys, vm);
        }

        private static Type DetectRootType(TextDocument doc, string filePath)
        {
            var ext = !string.IsNullOrEmpty(filePath) ? Path.GetExtension(filePath).ToLowerInvariant() : "";
            if (ext == ".pcby") return typeof(BoardModelDto);
            if (ext == ".stly") return typeof(List<StyleDto>);
            if (ext == ".layy") return typeof(LayerModelDto);

            // Если расширение файла не стандартное или файл еще не сохранен,
            // определяем тип по содержимому документа
            for (int l = 1; l <= Math.Min(doc.LineCount, 60); l++)
            {
                var line = doc.GetLineByNumber(l);
                var text = doc.GetText(line.Offset, line.Length).Trim();
                if (string.IsNullOrWhiteSpace(text) || text.StartsWith("#")) continue;

                if (text.StartsWith("layers:") || text.StartsWith("nets:") || text.StartsWith("vias:"))
                    return typeof(BoardModelDto);

                if (text.StartsWith("- !") && text.Contains("style"))
                    return typeof(List<StyleDto>);

                if (text.StartsWith("components:") || text.StartsWith("traces:") || text.StartsWith("shape:"))
                    return typeof(LayerModelDto);
            }

            return typeof(LayerModelDto);
        }

        private static int GetEffectiveIndent(TextDocument doc, int caretLineNumber)
        {
            var currentLine = doc.GetLineByNumber(caretLineNumber);
            var lineText = doc.GetText(currentLine.Offset, currentLine.Length);
            var trimmedCurrent = lineText.TrimStart();
            int currentLineIndent = GetIndentation(lineText);

            // Если на текущей строке явно начат элемент списка с дефиса ("- " или "-")
            if (trimmedCurrent.StartsWith("-"))
            {
                return currentLineIndent;
            }

            // Вычисляем контекстный отступ по предыдущей значимой строке
            for (int l = caretLineNumber - 1; l >= 1; l--)
            {
                var line = doc.GetLineByNumber(l);
                var text = doc.GetText(line.Offset, line.Length);
                var trimmed = text.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                    continue;

                int prevIndent = GetIndentation(text);

                // Если предыдущая строка - элемент списка, содержащий объект
                // (например, "  - name: NET" или "  - !resistor" или "  -")
                if (trimmed.StartsWith("- ") || trimmed == "-")
                {
                    if (trimmed.StartsWith("- !") || trimmed.Contains(':') || trimmed == "-")
                    {
                        int objectPropIndent = prevIndent + 2;
                        if (string.IsNullOrWhiteSpace(lineText))
                            return objectPropIndent;

                        return Math.Max(currentLineIndent, objectPropIndent);
                    }
                    return prevIndent;
                }

                // Если предыдущая строка открыла блок (заканчивается на ':' или ': !tag')
                if (trimmed.EndsWith(':') || trimmed.Contains(": !"))
                {
                    int childBlockIndent = prevIndent + 2;
                    if (string.IsNullOrWhiteSpace(lineText))
                        return childBlockIndent;

                    return Math.Max(currentLineIndent, childBlockIndent);
                }

                // Предыдущая строка - обычное свойство объекта
                if (string.IsNullOrWhiteSpace(lineText))
                    return prevIndent;

                return Math.Max(currentLineIndent, prevIndent);
            }

            return currentLineIndent;
        }

        private static (Type? TargetType, PropertyInfo? TargetProperty, int ObjectStartLine) ResolveEnclosingObject(
            TextDocument doc,
            int caretLineNumber,
            int effectiveIndent,
            Type rootType)
        {
            if (effectiveIndent <= 0)
            {
                return (rootType, null, 1);
            }

            // Ищем строку, открывшую текущий блок (отступ строго меньше effectiveIndent)
            for (int l = caretLineNumber - 1; l >= 1; l--)
            {
                var line = doc.GetLineByNumber(l);
                var text = doc.GetText(line.Offset, line.Length);
                var trimmed = text.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                    continue;

                int indent = GetIndentation(text);

                // Нас интересует строка с отступом строго меньше effectiveIndent
                if (indent >= effectiveIndent)
                    continue;

                // Вариант 1: Элемент списка (- !tag или - key: val или -)
                if (trimmed.StartsWith("- ") || trimmed == "-")
                {
                    var tag = FindTagInLine(trimmed);
                    if (!string.IsNullOrEmpty(tag))
                    {
                        if (YamlTagRegistry.LayerTagsMap.TryGetValue(tag, out var tagType) ||
                            YamlTagRegistry.BoardTagsMap.TryGetValue(tag, out tagType) ||
                            YamlTagRegistry.StylesTagsMap.TryGetValue(tag, out tagType))
                        {
                            return (tagType, null, l);
                        }
                    }

                    // Если тега нет, тип элемента определяется родительским свойством-списком (например, nets:)
                    var (parentContainerType, parentProp, _) = ResolveEnclosingObject(doc, l, indent, rootType);
                    if (parentContainerType != null)
                    {
                        var elemType = parentContainerType;
                        if (parentProp != null)
                        {
                            var pType = Nullable.GetUnderlyingType(parentProp.PropertyType) ?? parentProp.PropertyType;
                            if (pType.IsGenericType && pType.GetGenericTypeDefinition() == typeof(List<>))
                            {
                                elemType = Nullable.GetUnderlyingType(pType.GetGenericArguments()[0]) ?? pType.GetGenericArguments()[0];
                            }
                        }
                        return (elemType, parentProp, l);
                    }

                    return (null, null, l);
                }

                // Вариант 2: Свойство-блок (например, footprint: или shape: !rect или nets:)
                var key = ExtractKeyFromLine(trimmed);
                if (!string.IsNullOrEmpty(key))
                {
                    var tag = FindTagInLine(trimmed);
                    if (!string.IsNullOrEmpty(tag))
                    {
                        if (YamlTagRegistry.LayerTagsMap.TryGetValue(tag, out var tagType) ||
                            YamlTagRegistry.BoardTagsMap.TryGetValue(tag, out tagType) ||
                            YamlTagRegistry.StylesTagsMap.TryGetValue(tag, out tagType))
                        {
                            return (tagType, null, l);
                        }
                    }

                    var (parentType, _, _) = ResolveEnclosingObject(doc, l, indent, rootType);
                    if (parentType != null)
                    {
                        var prop = FindPropertyByAlias(parentType, key);
                        if (prop != null)
                        {
                            var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                            if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(List<>))
                            {
                                var itemType = Nullable.GetUnderlyingType(propType.GetGenericArguments()[0]) ?? propType.GetGenericArguments()[0];
                                return (itemType, prop, l);
                            }
                            return (propType, prop, l);
                        }
                    }
                }
            }

            return (rootType, null, 1);
        }

        private static HashSet<string> CollectExistingPropertyKeys(
            TextDocument doc,
            int caretLineNumber,
            int objectStartLine,
            int effectiveIndent,
            Type? targetDtoType)
        {
            // 1. Сначала определяем заданные свойства через C# AST (YamlStream из YamlDotNet)
            var astKeys = GetExistingKeysFromYamlAst(doc, caretLineNumber, objectStartLine, effectiveIndent, targetDtoType);
            if (astKeys != null)
            {
                return astKeys;
            }

            // 2. Фоллбек на построчное сканирование, если синтаксическое дерево не удалось построить
            var existingKeys = astKeys ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void RegisterKey(string rawKey)
            {
                if (string.IsNullOrWhiteSpace(rawKey)) return;
                existingKeys.Add(rawKey);
                if (targetDtoType != null)
                {
                    var prop = FindPropertyByAlias(targetDtoType, rawKey);
                    if (prop != null)
                    {
                        var yamlAttr = prop.GetCustomAttribute<YamlMemberAttribute>();
                        var propAlias = yamlAttr?.Alias ?? prop.Name.ToLowerInvariant();
                        existingKeys.Add(propAlias);
                        existingKeys.Add(prop.Name);
                    }
                }
            }

            // Проверяем строку начала объекта на наличие инлайн-свойства
            // (например, "  - name: NET", где свойство name записано прямо на строке элемента списка)
            if (objectStartLine >= 1 && objectStartLine <= doc.LineCount && objectStartLine != caretLineNumber)
            {
                var startLine = doc.GetLineByNumber(objectStartLine);
                var startText = doc.GetText(startLine.Offset, startLine.Length).Trim();
                if (startText.StartsWith("- "))
                {
                    var inlineKey = ExtractKeyFromLine(startText);
                    if (!string.IsNullOrEmpty(inlineKey))
                    {
                        RegisterKey(inlineKey);
                    }
                }
            }

            // Сканируем строки объекта ниже строки начала
            int scanStart = effectiveIndent == 0 ? 1 : objectStartLine + 1;
            for (int l = scanStart; l <= doc.LineCount; l++)
            {
                if (l == caretLineNumber)
                    continue;

                var line = doc.GetLineByNumber(l);
                var text = doc.GetText(line.Offset, line.Length);
                var trimmed = text.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                    continue;

                int lineIndent = GetIndentation(text);

                // Если строка имеет строго меньший отступ, чем свойства объекта - мы вышли из объекта
                if (lineIndent < effectiveIndent)
                {
                    break;
                }

                // Если встретили новый элемент списка на уровне родителя или нашего объекта - объект завершен
                if (effectiveIndent > 0 && (trimmed.StartsWith("- ") || trimmed == "-"))
                {
                    break;
                }

                // Если отступ совпадает с отступом свойств этого объекта - извлекаем ключ
                if (lineIndent == effectiveIndent)
                {
                    var key = ExtractKeyFromLine(trimmed);
                    if (!string.IsNullOrEmpty(key))
                    {
                        RegisterKey(key);
                    }
                }
            }

            return existingKeys;
        }

        private static HashSet<string>? GetExistingKeysFromYamlAst(
            TextDocument doc,
            int caretLine,
            int objectStartLine,
            int caretIndent,
            Type? targetDtoType)
        {
            try
            {
                // Санитизируем строку с кареткой, чтобы незавершенное слово не ломало парсер
                var caretLineObj = doc.GetLineByNumber(caretLine);
                var rawLineText = doc.GetText(caretLineObj.Offset, caretLineObj.Length);
                string textToParse = doc.Text;

                // Если на строке каретки нет двоеточия (пользователь только набирает имя свойства),
                // временно заменяем эту строку комментарием для синтаксически валидного дерева AST
                if (!rawLineText.Contains(':'))
                {
                    int lineStart = caretLineObj.Offset;
                    int lineLength = caretLineObj.Length;
                    textToParse = doc.Text.Remove(lineStart, lineLength).Insert(lineStart, new string(' ', GetIndentation(rawLineText)) + "#");
                }

                var stream = new YamlStream();
                using var reader = new StringReader(textToParse);
                stream.Load(reader);

                if (stream.Documents.Count == 0 || stream.Documents[0].RootNode == null)
                    return null;

                var root = stream.Documents[0].RootNode;
                var existingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                void RegisterKey(string rawKey)
                {
                    if (string.IsNullOrWhiteSpace(rawKey)) return;
                    existingKeys.Add(rawKey);
                    if (targetDtoType != null)
                    {
                        var prop = FindPropertyByAlias(targetDtoType, rawKey);
                        if (prop != null)
                        {
                            var yamlAttr = prop.GetCustomAttribute<YamlMemberAttribute>();
                            var propAlias = yamlAttr?.Alias ?? prop.Name.ToLowerInvariant();
                            existingKeys.Add(propAlias);
                            existingKeys.Add(prop.Name);
                        }
                    }
                }

                // Корневой объект (отступ 0)
                if (caretIndent == 0 && root is YamlMappingNode rootMap)
                {
                    foreach (var kvp in rootMap.Children)
                    {
                        if (kvp.Key is YamlScalarNode k && !string.IsNullOrWhiteSpace(k.Value))
                        {
                            if (k.Start.Line != caretLine)
                                RegisterKey(k.Value);
                        }
                    }
                    return existingKeys;
                }

                YamlMappingNode? targetMapping = null;

                void Search(YamlNode node)
                {
                    if (targetMapping != null) return;

                    if (node is YamlMappingNode map)
                    {
                        // Маппинг искомого объекта должен начинаться на строке открытия объекта
                        // (или на следующей строке, если дефис элемента списка был на отдельной строке)
                        if (map.Start.Line == objectStartLine || map.Start.Line == objectStartLine + 1)
                        {
                            targetMapping = map;
                            return;
                        }

                        foreach (var kvp in map.Children)
                        {
                            if (kvp.Value is YamlMappingNode or YamlSequenceNode)
                            {
                                Search(kvp.Value);
                                if (targetMapping != null) return;
                            }
                        }
                    }
                    else if (node is YamlSequenceNode seq)
                    {
                        foreach (var item in seq.Children)
                        {
                            Search(item);
                            if (targetMapping != null) return;
                        }
                    }
                }

                Search(root);

                if (targetMapping != null)
                {
                    foreach (var kvp in targetMapping.Children)
                    {
                        if (kvp.Key is YamlScalarNode k && !string.IsNullOrWhiteSpace(k.Value))
                        {
                            if (k.Start.Line != caretLine)
                                RegisterKey(k.Value);
                        }
                    }
                }

                return existingKeys;
            }
            catch
            {
                // При ошибке разбора AST вернется null и сработает надежный текстовый фоллбек
            }

            return null;
        }

        private static IReadOnlyList<QuartzCompletionData> ResolveKeyOrTagCompletions(
            TextEditor? editor,
            Type? containerType,
            PropertyInfo? containerProperty,
            bool isListItem,
            int currentIndent,
            string filePath,
            Type rootType,
            HashSet<string> existingKeys,
            EditorVM? vm = null)
        {
            if (containerType == null)
            {
                if (rootType == typeof(List<StyleDto>) || rootType == typeof(StyleDto))
                    return GetTagsAssignableTo(typeof(StyleDto), rootType, currentIndent, isListItem)
                        .Select(p => p.WithTargetIndent(currentIndent))
                        .ToList();

                return FilterExistingProperties(GetPropertiesForType(rootType), existingKeys)
                    .Select(p => p.WithTargetIndent(currentIndent))
                    .ToList();
            }

            // Если контекст - список строк (например, imports или layers)
            if (containerType == typeof(string))
            {
                var propName = containerProperty?.Name?.ToLowerInvariant() ?? "";
                if (propName.Contains("import"))
                    return GetProjectFileCompletions(editor, filePath, isListItem, vm, ".stly");
                if (propName.Contains("layer"))
                    return GetProjectFileCompletions(editor, filePath, isListItem, vm, ".layy");

                return Array.Empty<QuartzCompletionData>();
            }

            // Если контекст - базовый полиморфный класс с тегами (ComponentDto, StyleDto, SegmentDto, EndpointBaseDto, ShapeDto)
            if (containerType.IsAbstract && (
                containerType.IsAssignableTo(typeof(ComponentDto)) ||
                containerType.IsAssignableTo(typeof(StyleDto)) ||
                containerType.IsAssignableTo(typeof(SegmentDto)) ||
                containerType.IsAssignableTo(typeof(ShapeDto)) ||
                containerType.IsAssignableTo(typeof(EndpointBaseDto))))
            {
                return GetTagsAssignableTo(containerType, rootType, currentIndent, isListItem)
                    .Select(p => p.WithTargetIndent(currentIndent))
                    .ToList();
            }

            // Конкретный DTO: возвращаем все его свойства через рефлексию, отфильтровав уже заданные
            return FilterExistingProperties(GetPropertiesForType(containerType), existingKeys)
                .Select(p => p.WithTargetIndent(currentIndent))
                .ToList();
        }

        private static IReadOnlyList<QuartzCompletionData> FilterExistingProperties(
            IReadOnlyList<QuartzCompletionData> properties,
            HashSet<string> existingKeys)
        {
            if (existingKeys == null || existingKeys.Count == 0)
                return properties;

            return properties
                .Where(p => !existingKeys.Contains(p.Text))
                .ToList();
        }

        private static IReadOnlyList<QuartzCompletionData> ResolveValueCompletions(
            TextEditor? editor,
            string filePath,
            Type? containerType,
            string propertyAlias,
            Type rootType,
            EditorVM? vm = null)
        {
            if (containerType == null)
            {
                return GetFallbackValueCompletions(editor, filePath, propertyAlias, rootType, vm);
            }

            var prop = FindPropertyByAlias(containerType, propertyAlias);
            if (prop == null)
            {
                return GetFallbackValueCompletions(editor, filePath, propertyAlias, rootType, vm);
            }

            var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            if (propType.IsEnum)
            {
                return GetEnumCompletions(propType);
            }

            if (propType == typeof(bool))
            {
                return BooleanValues;
            }

            if (propType.IsAssignableTo(typeof(ShapeDto)) ||
                propType.IsAssignableTo(typeof(EndpointBaseDto)) ||
                propType.IsAssignableTo(typeof(ComponentDto)) ||
                propType.IsAssignableTo(typeof(StyleDto)))
            {
                return GetTagsAssignableTo(propType, rootType, currentIndent: 2, isListItem: true);
            }

            if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elemType = Nullable.GetUnderlyingType(propType.GetGenericArguments()[0]) ?? propType.GetGenericArguments()[0];
                if (elemType == typeof(string))
                {
                    if (propertyAlias.Contains("import", StringComparison.OrdinalIgnoreCase))
                        return GetProjectFileCompletions(editor, filePath, isListItem: true, vm, ".stly");
                    if (propertyAlias.Contains("layer", StringComparison.OrdinalIgnoreCase))
                        return GetProjectFileCompletions(editor, filePath, isListItem: true, vm, ".layy");
                }
                else if (elemType.IsAssignableTo(typeof(ComponentDto)) ||
                         elemType.IsAssignableTo(typeof(StyleDto)) ||
                         elemType.IsAssignableTo(typeof(ShapeDto)) ||
                         elemType.IsAssignableTo(typeof(EndpointBaseDto)))
                {
                    return GetTagsAssignableTo(elemType, rootType, currentIndent: 2, isListItem: true);
                }
            }

            return GetFallbackValueCompletions(editor, filePath, propertyAlias, rootType, vm);
        }

        private static IReadOnlyList<QuartzCompletionData> GetFallbackValueCompletions(
            TextEditor? editor,
            string filePath,
            string propertyAlias,
            Type rootType,
            EditorVM? vm = null)
        {
            var alias = propertyAlias.ToLowerInvariant();
            if (alias.Contains("import"))
                return GetProjectFileCompletions(editor, filePath, isListItem: true, vm, ".stly");
            if (alias.Contains("layer"))
                return GetProjectFileCompletions(editor, filePath, isListItem: true, vm, ".layy");

            return alias switch
            {
                "unit" => GetEnumCompletions(typeof(LengthUnit)),
                "mounting-type" => GetEnumCompletions(typeof(MountingType)),
                "coord-mode" => GetEnumCompletions(typeof(CoordinateMode)),
                "electrical-type" => GetEnumCompletions(typeof(PinElectricalType)),
                "is-plated" or "is-visible" or "clockwise" or "large-arc" => BooleanValues,
                "shape" => GetTagsAssignableTo(typeof(ShapeDto), rootType, currentIndent: 2, isListItem: true),
                "from" or "to" => GetTagsAssignableTo(typeof(EndpointBaseDto), rootType, currentIndent: 2, isListItem: true),
                _ => Array.Empty<QuartzCompletionData>()
            };
        }

        public static IReadOnlyList<QuartzCompletionData> GetPropertiesForType(Type type)
        {
            if (PropertyCache.TryGetValue(type, out var cached))
                return cached;

            var list = new List<QuartzCompletionData>();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                if (prop.GetCustomAttribute<YamlIgnoreAttribute>() != null)
                    continue;

                var yamlAttr = prop.GetCustomAttribute<YamlMemberAttribute>();
                var alias = yamlAttr?.Alias ?? prop.Name.ToLowerInvariant();

                if (alias.Equals("line", StringComparison.OrdinalIgnoreCase) ||
                    alias.Equals("column", StringComparison.OrdinalIgnoreCase) ||
                    alias.Equals("length", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                var isRequired = prop.GetCustomAttribute<RequiredAttribute>() != null;

                var (icon, detail, snippet) = GetPropertyMetadata(alias, propType, isRequired);
                list.Add(new QuartzCompletionData(alias, icon, detail, snippet));
            }

            PropertyCache[type] = list;
            return list;
        }

        private static (LucideIconKind Icon, string Detail, string Snippet) GetPropertyMetadata(string alias, Type propType, bool isRequired)
        {
            string reqSuffix = isRequired ? " (Req)" : "";

            if (propType.IsEnum)
            {
                var firstVal = Enum.GetNames(propType).FirstOrDefault() ?? "";
                return (LucideIconKind.Tag, propType.Name + reqSuffix, $"{alias}: {firstVal}");
            }

            if (propType == typeof(bool))
            {
                return (LucideIconKind.Check, "Boolean" + reqSuffix, $"{alias}: true");
            }

            if (propType == typeof(Point2DDto))
            {
                return (LucideIconKind.Move, "Point2D" + reqSuffix, $"{alias}:\n  x: 0\n  y: 0");
            }

            if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elemType = Nullable.GetUnderlyingType(propType.GetGenericArguments()[0]) ?? propType.GetGenericArguments()[0];
                var icon = GetListIcon(alias);
                var detail = $"List<{elemType.Name.Replace("Dto", "")}>{reqSuffix}";

                string snippet;
                if (elemType == typeof(Point2DDto))
                    snippet = $"{alias}:\n  - x: 0\n    y: 0";
                else if (elemType.IsAssignableTo(typeof(ComponentDto)))
                    snippet = $"{alias}:\n  - !resistor\n    name: ";
                else if (elemType.IsAssignableTo(typeof(StyleDto)))
                    snippet = $"{alias}:\n  - !";
                else
                    snippet = $"{alias}:\n  - ";

                return (icon, detail, snippet);
            }

            if (propType == typeof(double) || propType == typeof(float) || propType == typeof(int) || propType == typeof(long))
            {
                var icon = GetNumberIcon(alias);
                return (icon, "Number" + reqSuffix, $"{alias}: ");
            }

            if (propType == typeof(string))
            {
                var icon = alias.Contains("net") ? LucideIconKind.Route : LucideIconKind.Tag;
                return (icon, "String" + reqSuffix, $"{alias}: ");
            }

            if (propType.IsAssignableTo(typeof(ShapeDto)))
            {
                return (LucideIconKind.Square, "Shape" + reqSuffix, $"{alias}: !rect\n  ");
            }

            if (propType.IsAssignableTo(typeof(EndpointBaseDto)))
            {
                return (LucideIconKind.CircleDot, "Endpoint" + reqSuffix, $"{alias}: !via\n  ");
            }

            var complexIcon = alias.Contains("style") ? LucideIconKind.Sparkles :
                              alias.Contains("footprint") ? LucideIconKind.Cpu :
                              LucideIconKind.Box;

            return (complexIcon, propType.Name.Replace("Dto", "") + reqSuffix, $"{alias}:\n  ");
        }

        public static IReadOnlyList<QuartzCompletionData> GetEnumCompletions(Type enumType)
        {
            if (EnumCache.TryGetValue(enumType, out var cached))
                return cached;

            var list = new List<QuartzCompletionData>();
            foreach (var name in Enum.GetNames(enumType))
            {
                var text = enumType == typeof(LengthUnit) ? name.ToLowerInvariant() : name;
                list.Add(new QuartzCompletionData(text, LucideIconKind.Tag, enumType.Name, text));
            }

            EnumCache[enumType] = list;
            return list;
        }

        public static IReadOnlyDictionary<string, Type> GetTagMapForRootType(Type rootType)
        {
            if (rootType == typeof(BoardModelDto))
                return YamlTagRegistry.BoardTagsMap;
            if (rootType == typeof(LayerModelDto))
                return YamlTagRegistry.LayerTagsMap;
            if (rootType == typeof(List<StyleDto>) || rootType == typeof(StyleDto))
                return YamlTagRegistry.StylesTagsMap;

            return YamlTagRegistry.StylesTagsMap;
        }

        public static IReadOnlyList<QuartzCompletionData> GetTagsAssignableTo(
            Type baseType,
            Type rootType,
            int currentIndent = 2,
            bool isListItem = true)
        {
            int childIndent = 2;
            string pad = new string(' ', childIndent);
            string prefix = isListItem ? "" : "- ";
            var tagMap = GetTagMapForRootType(rootType);

            return tagMap
                .Where(kvp => kvp.Value.IsAssignableTo(baseType))
                .Select(kvp =>
                {
                    var (icon, category, snippet) = GetTagMetadata(kvp.Key, kvp.Value, pad, prefix);
                    return new QuartzCompletionData(kvp.Key, icon, category, snippet);
                })
                .ToList();
        }

        private static (LucideIconKind Icon, string Category, string Snippet) GetTagMetadata(
            string tag,
            Type dtoType,
            string pad = "    ",
            string prefix = "")
        {
            if (dtoType.IsAssignableTo(typeof(ComponentDto)))
                return (LucideIconKind.Cpu, "Component", $"{prefix}{tag}\n{pad}name: ");

            if (dtoType.IsAssignableTo(typeof(StyleDto)))
                return (LucideIconKind.Sparkles, "Style", $"{prefix}{tag}\n{pad}name: ");

            if (dtoType.IsAssignableTo(typeof(ShapeDto)))
                return (LucideIconKind.Square, "Shape", $"{prefix}{tag}\n{pad}");

            if (dtoType == typeof(LineSegmentDto))
                return (LucideIconKind.Spline, "Segment", $"{prefix}{tag}\n{pad}point:\n{pad}  x: 0\n{pad}  y: 0");

            if (dtoType == typeof(ArcSegmentDto))
                return (LucideIconKind.RotateCw, "Segment", $"{prefix}{tag}\n{pad}point:\n{pad}  x: 0\n{pad}  y: 0\n{pad}radius: ");

            if (dtoType.IsAssignableTo(typeof(EndpointBaseDto)))
                return (LucideIconKind.CircleDot, "Endpoint", $"{prefix}{tag}\n{pad}");

            return (LucideIconKind.Tag, dtoType.Name.Replace("Dto", ""), $"{prefix}{tag}\n{pad}");
        }

        private static PropertyInfo? FindPropertyByAlias(Type type, string alias)
        {
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var yamlAttr = prop.GetCustomAttribute<YamlMemberAttribute>();
                var propAlias = yamlAttr?.Alias ?? prop.Name;
                if (string.Equals(propAlias, alias, StringComparison.OrdinalIgnoreCase))
                    return prop;
            }
            return null;
        }

        private static string? FindTagInLine(string trimmed)
        {
            var match = Regex.Match(trimmed, @"![\w-]+");
            return match.Success ? match.Value : null;
        }

        private static string? ExtractKeyFromLine(string trimmed)
        {
            var line = trimmed.TrimStart('-', ' ').Trim();
            int colon = line.IndexOf(':');
            if (colon > 0)
            {
                return line[..colon].Trim().Trim('"', '\'');
            }
            return null;
        }

        private static int GetIndentation(string line)
        {
            int count = 0;
            foreach (char c in line)
            {
                if (c == ' ') count++;
                else if (c == '\t') count += 2;
                else break;
            }
            return count;
        }

        private static IReadOnlyList<QuartzCompletionData> GetProjectFileCompletions(
            TextEditor? editor,
            string currentFilePath,
            bool isListItem,
            EditorVM? vm = null,
            params string[] allowedExtensions)
        {
            var results = new List<QuartzCompletionData>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var resolvedVm = vm ?? editor?.DataContext as EditorVM;
            var mainVm = resolvedVm?.MainVM;

            // 1. Сначала берем файлы, уже зарегистрированные в MainVM проекта
            if (mainVm != null)
            {
                if (allowedExtensions.Contains(".layy"))
                {
                    foreach (var relPath in mainVm.GetAllLayerTexts().Keys)
                    {
                        AddFileCompletion(results, seen, relPath, ".layy", isListItem);
                    }
                }

                if (allowedExtensions.Contains(".stly"))
                {
                    foreach (var relPath in mainVm.GetAllStylesTexts().Keys)
                    {
                        AddFileCompletion(results, seen, relPath, ".stly", isListItem);
                    }
                }
            }

            // 2. Сканируем файловую систему проекта рекурсивно
            try
            {
                var projectDir = mainVm?.ProjectRootDir;
                if (string.IsNullOrEmpty(projectDir) || !Directory.Exists(projectDir))
                {
                    projectDir = !string.IsNullOrEmpty(currentFilePath)
                        ? Path.GetDirectoryName(currentFilePath)
                        : null;
                }

                if (!string.IsNullOrEmpty(projectDir) && Directory.Exists(projectDir))
                {
                    foreach (var ext in allowedExtensions)
                    {
                        var files = Directory.GetFiles(projectDir, $"*{ext}", SearchOption.AllDirectories);
                        foreach (var fullPath in files)
                        {
                            var relPath = Path.GetRelativePath(projectDir, fullPath).Replace('\\', '/');
                            AddFileCompletion(results, seen, relPath, ext, isListItem);
                        }
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки файловой системы
            }

            return results;
        }

        private static void AddFileCompletion(
            List<QuartzCompletionData> results,
            HashSet<string> seen,
            string relPath,
            string ext,
            bool isListItem)
        {
            if (string.IsNullOrWhiteSpace(relPath) || !seen.Add(relPath))
                return;

            var icon = ext.Equals(".layy", StringComparison.OrdinalIgnoreCase)
                ? LucideIconKind.Layers
                : LucideIconKind.Sparkles;

            var category = ext.Equals(".layy", StringComparison.OrdinalIgnoreCase)
                ? "Layer"
                : "Style";

            string pathValue = relPath.Contains(' ') ? $"\"{relPath}\"" : relPath;
            string insertText = isListItem ? pathValue : $"- {pathValue}";

            results.Add(new QuartzCompletionData(
                relPath,
                icon,
                category,
                insertText));
        }

        private static LucideIconKind GetListIcon(string alias)
        {
            if (alias.Contains("layers")) return LucideIconKind.Layers;
            if (alias.Contains("components")) return LucideIconKind.Box;
            if (alias.Contains("traces") || alias.Contains("nets") || alias.Contains("nodes")) return LucideIconKind.Route;
            if (alias.Contains("vias")) return LucideIconKind.Disc;
            if (alias.Contains("pads") || alias.Contains("pins")) return LucideIconKind.CircleDot;
            if (alias.Contains("segments")) return LucideIconKind.Spline;
            if (alias.Contains("imports")) return LucideIconKind.FileCode;
            if (alias.Contains("styles")) return LucideIconKind.Sparkles;
            return LucideIconKind.List;
        }

        private static LucideIconKind GetNumberIcon(string alias)
        {
            if (alias.Contains("radius")) return LucideIconKind.Circle;
            if (alias.Contains("angle")) return LucideIconKind.RotateCw;
            if (alias.Contains("width") || alias.Contains("height") || alias.Contains("diameter") || alias.Contains("size"))
                return LucideIconKind.Ruler;
            return LucideIconKind.Variable;
        }
    }
}
