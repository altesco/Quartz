using System;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using Lucide.Avalonia;

namespace Quartz.UI.CodeCompletion
{
    public class QuartzCompletionData : ICompletionData
    {
        public LucideIconKind IconKind { get; }
        public string Text { get; }
        public string? Detail { get; }
        public string? InsertText { get; }
        public object Content => this;
        public object? Description { get; }
        public double Priority { get; }
        public int? TargetIndent { get; }

        public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

        public IImage? Image => null;

        public QuartzCompletionData(
            string text,
            LucideIconKind iconKind = LucideIconKind.CodeXml,
            string? detail = null,
            string? insertText = null,
            object? description = null,
            double priority = 0,
            int? targetIndent = null)
        {
            Text = text;
            IconKind = iconKind;
            Detail = detail;
            InsertText = insertText;
            Description = description;
            Priority = priority;
            TargetIndent = targetIndent;
        }

        public QuartzCompletionData WithTargetIndent(int targetIndent)
        {
            return new QuartzCompletionData(
                Text,
                IconKind,
                Detail,
                InsertText,
                Description,
                Priority,
                targetIndent);
        }

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            var textToInsert = InsertText ?? Text;
            if (completionSegment.Offset > 0 && textArea.Document.GetCharAt(completionSegment.Offset - 1) == '-')
            {
                textToInsert = " " + textToInsert;
            }

            var doc = textArea.Document;
            var line = doc.GetLineByOffset(completionSegment.Offset);
            var lineText = doc.GetText(line.Offset, line.Length);
            int wordOffsetInLine = Math.Max(0, completionSegment.Offset - line.Offset);
            string textBeforeWord = wordOffsetInLine > 0 ? lineText[..wordOffsetInLine] : string.Empty;

            if (TargetIndent.HasValue && string.IsNullOrWhiteSpace(textBeforeWord))
            {
                int expectedIndent = TargetIndent.Value;
                string pad = new string(' ', expectedIndent);

                if (textToInsert.Contains('\n'))
                {
                    var lines = textToInsert.Split('\n');
                    for (int i = 1; i < lines.Length; i++)
                    {
                        lines[i] = pad + lines[i];
                    }
                    textToInsert = string.Join('\n', lines);
                }

                int replaceStart = line.Offset;
                int replaceLength = Math.Max(0, (completionSegment.Offset + completionSegment.Length) - line.Offset);

                doc.Replace(replaceStart, replaceLength, pad + textToInsert);
                textArea.Caret.Offset = replaceStart + pad.Length + textToInsert.Length;
                return;
            }

            if (textToInsert.Contains('\n'))
            {
                int indentLen = 0;
                while (indentLen < lineText.Length && (lineText[indentLen] == ' ' || lineText[indentLen] == '\t'))
                {
                    indentLen++;
                }
                var indent = lineText[..indentLen];

                var lines = textToInsert.Split('\n');
                for (int i = 1; i < lines.Length; i++)
                {
                    lines[i] = indent + lines[i];
                }
                textToInsert = string.Join('\n', lines);
            }

            doc.Replace(completionSegment, textToInsert);
            textArea.Caret.Offset = completionSegment.Offset + textToInsert.Length;
        }
    }
}

