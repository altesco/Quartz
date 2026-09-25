using Quartz.Core.Enums;
using Quartz.Core.Models.BoardEntities.Styles;
using Quartz.Infrastructure.Dtos.Styles;
using Quartz.Core.Models;

namespace Quartz.Infrastructure.Tools;

public static class YamlParserHelpers
{
    public static string NormalizeEmptyTaggedObjects(string yamlText, HashSet<string> tags)
    {
        var lines = yamlText.Split('\n');
        var result = new List<string>(lines.Length);

        for (int i = 0; i < lines.Length; i++)
        {
            string currentLine = lines[i];
            string trimmed = currentLine.Trim();

            if (trimmed.Length == 0 || trimmed.StartsWith("#"))
            {
                result.Add(currentLine);
                continue;
            }

            int currentIndent = GetIndentation(currentLine);
            string? tag = GetEmptyObjectTag(trimmed, tags);

            if (tag == null)
            {
                result.Add(currentLine);
                continue;
            }

            int nextIndex = i + 1;
            while (nextIndex < lines.Length)
            {
                string nextLine = lines[nextIndex];
                if (!string.IsNullOrWhiteSpace(nextLine) && !nextLine.TrimStart().StartsWith("#"))
                {
                    break;
                }

                nextIndex++;
            }

            bool hasNestedContent = false;
            if (nextIndex < lines.Length)
            {
                int nextIndent = GetIndentation(lines[nextIndex]);
                hasNestedContent = nextIndent > currentIndent;
            }

            if (!hasNestedContent)
            {
                result.Add(currentLine + " {}");
            }
            else
            {
                result.Add(currentLine);
            }
        }

        return string.Join('\n', result);
    }

    public static Dictionary<string, Style> ParseLocalStyles(
        List<StyleDto?>? dtoStyles,
        LengthUnit unit,
        out List<EditorError> errors)
    {
        List<EditorError> localErrors = [];
        var stylesMap = new Dictionary<string, Style>(StringComparer.OrdinalIgnoreCase);

        if (dtoStyles is null || dtoStyles.Count == 0)
        {
            errors = localErrors;
            return stylesMap;
        }

        var level1Shapes = new List<ShapeStyleDto>();
        var level2SubStyles = new List<StyleDto>();
        var level3MainStyles = new List<StyleDto>();

        foreach (var styleDto in dtoStyles)
        {
            if (styleDto == null)
                continue;
            
            switch (styleDto)
            {
                case ShapeStyleDto shape: level1Shapes.Add(shape); break;
                case PinStyleDto or PadStyleDto or ViaStyleDto: level2SubStyles.Add(styleDto); break;
                default: level3MainStyles.Add(styleDto); break;
            }
        }

        void TryRegisterStyle(Style? domain, StyleDto dto)
        {
            if (domain != null && !string.IsNullOrWhiteSpace(domain.Name))
            {
                if (!stylesMap.TryAdd(domain.Name, domain))
                    localErrors.AddError($"Дубликат Name стиля: {domain.Name}", dto.Line, dto.Column, dto.Length);
            }
        }

        foreach (var dto in level1Shapes)
        {
            TryRegisterStyle(dto.ToDomain(unit, out var e), dto);
            localErrors.AddRange(e);
        }

        foreach (var dto in level2SubStyles)
        {
            TryRegisterStyle(dto.ToDomain(unit, stylesMap, out var e), dto);
            localErrors.AddRange(e);
        }

        foreach (var dto in level3MainStyles)
        {
            TryRegisterStyle(dto.ToDomain(unit, stylesMap, out var e), dto);
            localErrors.AddRange(e);
        }

        errors = localErrors;
        return stylesMap;
    }

    public static string? GetEmptyObjectTag(string trimmedLine, IEnumerable<string> tags)
    {
        foreach (string tag in tags)
        {
            if (!trimmedLine.EndsWith(tag, StringComparison.Ordinal))
                continue;

            int tagStart = trimmedLine.Length - tag.Length;

            if (tagStart > 0)
            {
                char previous = trimmedLine[tagStart - 1];
                if (!char.IsWhiteSpace(previous) && previous != ':' && previous != '-')
                    continue;
            }

            return tag;
        }

        return null;
    }

    public static int GetIndentation(string line)
    {
        int count = 0;
        foreach (char c in line)
        {
            if (c is ' ' or '\t')
            {
                count++;
                continue;
            }

            break;
        }

        return count;
    }
}