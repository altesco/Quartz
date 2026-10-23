using System;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Editing;

namespace Quartz.UI.CodeCompletion
{
    /// <summary>
    /// Custom completion popup window styled to match Quartz context menu / flyout.
    /// Compensates offsets so the shadowed card inside aligns perfectly with the editor caret.
    /// </summary>
    public class QuartzCompletionWindow : CompletionWindow
    {
        private bool _isAdjusting;

        public Avalonia.Media.FontFamily? FontFamily
        {
            get => CompletionList.FontFamily;
            set
            {
                if (value != null)
                {
                    CompletionList.FontFamily = value;
                }
            }
        }

        public QuartzCompletionWindow(TextArea textArea) : base(textArea)
        {
            CloseWhenCaretAtBeginning = false;
            TakesFocusFromNativeControl = false;
            ShouldUseOverlayLayer = true;

            var editorFont = (textArea.GetService(typeof(TextEditor)) as TextEditor)?.FontFamily
                             ?? textArea.FontFamily;
            if (editorFont != null)
            {
                FontFamily = editorFont;
                CompletionList.FontFamily = editorFont;
            }

            Opened += OnWindowOpened;
            textArea.TextInput += OnTextAreaTextInput;
        }

        private void OnWindowOpened(object? sender, EventArgs e)
        {
            SelectFirstItem();
        }

        private void OnTextAreaTextInput(object? sender, TextInputEventArgs e)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (IsOpen)
                {
                    if (CompletionList.CurrentList.Count == 0)
                    {
                        Close();
                    }
                    else if (CompletionList.SelectedItem == null)
                    {
                        SelectFirstItem();
                    }
                }
            }, Avalonia.Threading.DispatcherPriority.Input);
        }

        public void SelectFirstItem()
        {
            var first = CompletionList.CurrentList.FirstOrDefault()
                     ?? CompletionList.CompletionData.FirstOrDefault();
            if (first != null)
            {
                CompletionList.SelectedItem = first;
                if (CompletionList.ListBox != null && CompletionList.ListBox.ItemCount > 0)
                {
                    CompletionList.ListBox.SelectedIndex = 0;
                }
            }
        }

        protected override void DetachEvents()
        {
            Opened -= OnWindowOpened;
            TextArea.TextInput -= OnTextAreaTextInput;
            base.DetachEvents();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (CompletionList.CurrentList.Count == 0)
            {
                Close();
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (_isAdjusting) return;

            // LayoutRoot inside cc:CompletionList template has Margin="12" for shadow rendering.
            // When CompletionWindowBase sets HorizontalOffset / VerticalOffset, we adjust by 12px
            // so the visual border aligns directly with the text cursor.
            if (change.Property == HorizontalOffsetProperty)
            {
                _isAdjusting = true;
                HorizontalOffset = change.GetNewValue<double>() - 12;
                _isAdjusting = false;
            }
            else if (change.Property == VerticalOffsetProperty)
            {
                _isAdjusting = true;
                // If popup opens upward, adjust for top margin, otherwise bottom margin
                double yAdjustment = IsUp ? -14 : -8;
                VerticalOffset = change.GetNewValue<double>() + yAdjustment;
                _isAdjusting = false;
            }
        }
    }
}

