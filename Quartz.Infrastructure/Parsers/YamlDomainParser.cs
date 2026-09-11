using Quartz.Core.Interfaces;
using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities;
using Quartz.Infrastructure.Dtos;
using Quartz.Infrastructure.Tools;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.NodeDeserializers;

namespace Quartz.Infrastructure.Parsers;

public class YamlDomainParser : IYamlDomainParser
{
    private readonly IDeserializer _deserializer;

    // ============================================================
    // SUPPORTED TAGS
    // ============================================================

    private static readonly HashSet<string> SupportedTags =
        new(StringComparer.Ordinal)
        {
            "!rect",
            "!path",

            "!resistor",
            "!capacitor",
            "!transistor",
            "!diode",
            "!inductor",
            "!ic",
            "!connector",

            "!trace",

            "!line",
            "!arc"
        };


    // ============================================================
    // CONSTRUCTOR
    // ============================================================

    public YamlDomainParser()
    {
        _deserializer =
            new DeserializerBuilder()
                .WithNamingConvention(
                    HyphenatedNamingConvention.Instance)
                .IgnoreUnmatchedProperties()

                // ------------------------------------------------
                // Перехватываем стандартный ObjectNodeDeserializer
                // только для установки Line / Column / Length.
                // ------------------------------------------------
                .WithNodeDeserializer(
                    inner =>
                        new PositionNodeDeserializer(inner),
                    s =>
                        s.InsteadOf<ObjectNodeDeserializer>())

                // ------------------------------------------------
                // COMPONENT TAGS
                // ------------------------------------------------
                .WithTagMapping(
                    "!resistor",
                    typeof(ResistorDto))
                .WithTagMapping(
                    "!capacitor",
                    typeof(CapacitorDto))
                .WithTagMapping(
                    "!transistor",
                    typeof(TransistorDto))
                .WithTagMapping(
                    "!diode",
                    typeof(DiodeDto))
                .WithTagMapping(
                    "!inductor",
                    typeof(InductorDto))
                .WithTagMapping(
                    "!ic",
                    typeof(IntegratedCircuitDto))
                .WithTagMapping(
                    "!connector",
                    typeof(ConnectorDto))

                // ------------------------------------------------
                // SHAPE TAGS
                // ------------------------------------------------
                .WithTagMapping(
                    "!rect",
                    typeof(RectShapeDto))
                .WithTagMapping(
                    "!path",
                    typeof(PathShapeDto))

                // ------------------------------------------------
                // SEGMENT TAGS
                // ------------------------------------------------
                .WithTagMapping(
                    "!line",
                    typeof(LineSegmentDto))
                .WithTagMapping(
                    "!arc",
                    typeof(ArcSegmentDto))

                // ------------------------------------------------
                // TRACE
                // ------------------------------------------------
                .WithTagMapping(
                    "!trace",
                    typeof(TraceDto))
                .Build();
    }


    // ============================================================
    // PARSE
    // ============================================================

    public LayerModel? Parse(
        string yamlText,
        out List<EditorError> errors)
    {
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

        // ------------------------------------------------------------
        // Проверяем только пользовательские теги.
        // Синтаксис уже проверяется JS-парсером.
        // ------------------------------------------------------------

        if (!ValidateTags(
                yamlText,
                errors))
        {
            return null;
        }

        string normalizedYaml =
            NormalizeEmptyTaggedObjects(
                yamlText);

        try
        {
            var dto =
                _deserializer.Deserialize<LayerModelDto?>(
                    normalizedYaml);

            if (dto == null)
            {
                errors.Add(new EditorError
                {
                    Message =
                        "Не удалось создать модель документа",

                    Line = 1,
                    Column = 1,
                    Length = 1
                });

                return null;
            }

            return dto.ToDomain(
                out errors);
        }
        catch (YamlException ex)
        {
            errors.Add(new EditorError
            {
                Message =
                    $"Ошибка десериализации YAML: " +
                    $"{ex.InnerException?.Message ?? ex.Message}",

                Line =
                    Math.Max(
                        1,
                        (int)ex.Start.Line),

                Column =
                    Math.Max(
                        1,
                        (int)ex.Start.Column),

                Length = 1
            });

            return null;
        }
    }


    // ============================================================
    // VALIDATE TAGS
    // ============================================================

    private static bool ValidateTags(
        string yamlText,
        List<EditorError> errors)
    {
        try
        {
            using var stringReader =
                new StringReader(yamlText);


            var parser =
                new Parser(stringReader);


            while (parser.MoveNext())
            {
                if (parser.Current is not NodeEvent node)
                    continue;


                if (node.Tag.IsEmpty)
                    continue;


                string tag =
                    node.Tag.Value ?? string.Empty;


                if (string.IsNullOrWhiteSpace(tag))
                    continue;


                if (SupportedTags.Contains(tag))
                    continue;


                errors.Add(new EditorError
                {
                    Message =
                        $"Неизвестный тэг элемента: '{tag}'",

                    Line =
                        Math.Max(
                            1,
                            (int)node.Start.Line),

                    Column =
                        Math.Max(
                            1,
                            (int)node.Start.Column),

                    Length =
                        Math.Max(
                            1,
                            tag.Length)
                });
            }


            return errors.Count == 0;
        }
        catch (YamlException ex)
        {
            errors.Add(new EditorError
            {
                Message =
                    $"Синтаксическая ошибка YAML: " +
                    $"{ex.InnerException?.Message ?? ex.Message}",

                Line =
                    Math.Max(
                        1,
                        (int)ex.Start.Line),

                Column =
                    Math.Max(
                        1,
                        (int)ex.Start.Column),

                Length = 1
            });

            return false;
        }
    }


    // ============================================================
    // NORMALIZE EMPTY TAGGED OBJECTS
    // ============================================================

    private static string NormalizeEmptyTaggedObjects(
        string yamlText)
    {
        var lines =
            yamlText.Split('\n');

        var result =
            new List<string>(
                lines.Length);


        for (int i = 0;
             i < lines.Length;
             i++)
        {
            string currentLine =
                lines[i];

            string trimmed =
                currentLine.Trim();


            // --------------------------------------------------------
            // Empty
            // --------------------------------------------------------

            if (trimmed.Length == 0)
            {
                result.Add(currentLine);
                continue;
            }


            // --------------------------------------------------------
            // Comment
            // --------------------------------------------------------

            if (trimmed.StartsWith("#"))
            {
                result.Add(currentLine);
                continue;
            }


            // --------------------------------------------------------
            // Indentation
            // --------------------------------------------------------

            int currentIndent =
                GetIndentation(currentLine);


            // --------------------------------------------------------
            // Is this an empty tagged object?
            // --------------------------------------------------------

            string? tag =
                GetEmptyObjectTag(trimmed);


            if (tag == null)
            {
                result.Add(currentLine);
                continue;
            }


            // --------------------------------------------------------
            // Find next meaningful line.
            // --------------------------------------------------------

            int nextIndex =
                i + 1;


            while (nextIndex < lines.Length)
            {
                string nextLine =
                    lines[nextIndex];


                if (!string.IsNullOrWhiteSpace(nextLine) &&
                    !nextLine.TrimStart().StartsWith("#"))
                {
                    break;
                }


                nextIndex++;
            }


            // --------------------------------------------------------
            // Does this tag have nested YAML?
            // --------------------------------------------------------

            bool hasNestedContent =
                false;


            if (nextIndex < lines.Length)
            {
                int nextIndent =
                    GetIndentation(
                        lines[nextIndex]);


                hasNestedContent =
                    nextIndent > currentIndent;
            }


            // --------------------------------------------------------
            // Only empty tags receive {}.
            //
            // !resistor        -> !resistor {}
            // !rect            -> !rect {}
            // !line            -> !line {}
            //
            // But:
            //
            // !resistor
            //   name: R1
            //
            // stays untouched.
            // --------------------------------------------------------

            if (!hasNestedContent)
            {
                result.Add(
                    currentLine + " {}");
            }
            else
            {
                result.Add(currentLine);
            }
        }


        return string.Join(
            '\n',
            result);
    }

    private static string? GetEmptyObjectTag(
        string trimmedLine)
    {
        string[] tags =
        [
            // Shapes
            "!rect",
            "!path",

            // Segments
            "!line",
            "!arc",

            // Components
            "!resistor",
            "!capacitor",
            "!transistor",
            "!diode",
            "!inductor",
            "!ic",
            "!connector",

            // Trace
            "!trace"
        ];


        foreach (string tag in tags)
        {
            if (!trimmedLine.EndsWith(
                    tag,
                    StringComparison.Ordinal))
            {
                continue;
            }


            int tagStart =
                trimmedLine.Length - tag.Length;


            if (tagStart > 0)
            {
                char previous =
                    trimmedLine[tagStart - 1];


                if (!char.IsWhiteSpace(previous) &&
                    previous != ':' &&
                    previous != '-')
                {
                    continue;
                }
            }


            return tag;
        }


        return null;
    }

    private static int GetIndentation(
        string line)
    {
        int count = 0;

        foreach (char c in line)
        {
            if (c == ' ')
            {
                count++;
                continue;
            }

            if (c == '\t')
            {
                // YAML обычно не любит табы для indentation,
                // но на всякий случай считаем их как один уровень.
                count++;
                continue;
            }

            break;
        }

        return count;
    }


    // ============================================================
    // POSITION NODE DESERIALIZER
    // ============================================================

    private sealed class PositionNodeDeserializer
        : INodeDeserializer
    {
        private readonly INodeDeserializer _inner;


        public PositionNodeDeserializer(
            INodeDeserializer inner)
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


            Mark? start =
                reader.Current?.Start;


            try
            {
                if (_inner.Deserialize(
                        reader,
                        expectedType,
                        nestedObjectDeserializer,
                        out value,
                        rootDeserializer))
                {
                    SetPosition(
                        value,
                        start,
                        reader.Current?.Start);

                    return true;
                }
            }
            catch
            {
                // При неполном YAML во время редактирования
                // YamlDotNet может бросить исключение.
            }


            return false;
        }


        // ========================================================
        // SET POSITION
        // ========================================================

        private static void SetPosition(
            object? value,
            Mark? start,
            Mark? end = null)
        {
            if (value is not BoardEntityDto entity ||
                !start.HasValue)
            {
                return;
            }


            entity.Line =
                Math.Max(
                    1,
                    (int)start.Value.Line);


            entity.Column =
                Math.Max(
                    1,
                    (int)start.Value.Column);


            if (end.HasValue &&
                end.Value.Index >=
                start.Value.Index)
            {
                entity.Length =
                    Math.Max(
                        1,
                        (int)(
                            end.Value.Index -
                            start.Value.Index));
            }
            else
            {
                entity.Length = 1;
            }
        }
    }
}