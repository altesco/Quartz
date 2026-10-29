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
            var doc = textArea.Document;

            if (!textToInsert.StartsWith(' ') && completionSegment.Offset > 0 && completionSegment.Offset <= doc.TextLength && doc.GetCharAt(completionSegment.Offset - 1) == '-')
            {
                textToInsert = " " + textToInsert;
            }

            int safeOffset = Math.Clamp(completionSegment.Offset, 0, doc.TextLength);
            var line = doc.GetLineByOffset(safeOffset);
            var lineText = doc.GetText(line.Offset, line.Length);
            int wordOffsetInLine = Math.Clamp(safeOffset - line.Offset, 0, line.Length);
            string textBeforeWord = lineText[..wordOffsetInLine];

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
                replaceStart = Math.Clamp(replaceStart, 0, doc.TextLength);
                replaceLength = Math.Clamp(replaceLength, 0, doc.TextLength - replaceStart);

                doc.Replace(replaceStart, replaceLength, pad + textToInsert);
                int targetOffset = Math.Clamp(replaceStart + pad.Length + textToInsert.Length, 0, doc.TextLength);
                textArea.Caret.Offset = targetOffset;
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

            int currentCaret = textArea.Caret.Offset;
            int segOffset = Math.Clamp(completionSegment.Offset, 0, doc.TextLength);
            int segLength = Math.Max(0, completionSegment.Length);
            if (currentCaret >= segOffset)
            {
                segLength = Math.Max(segLength, currentCaret - segOffset);
            }
            segLength = Math.Clamp(segLength, 0, doc.TextLength - segOffset);

            doc.Replace(segOffset, segLength, textToInsert);
            int finalCaret = Math.Clamp(segOffset + textToInsert.Length, 0, doc.TextLength);
            textArea.Caret.Offset = finalCaret;
        }
    }
}

