using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
        private IDisposable? _itemCountSubscription;

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

        public bool IsEmpty => (CompletionList.ListBox != null && CompletionList.ListBox.ItemCount == 0)
                            || (CompletionList.ListBox == null && CompletionList.CompletionData.Count == 0);

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
            CompletionList.TemplateApplied += OnCompletionListTemplateApplied;
        }

        private void OnCompletionListTemplateApplied(object? sender, TemplateAppliedEventArgs e)
        {
            _itemCountSubscription?.Dispose();
            if (CompletionList.ListBox != null)
            {
                _itemCountSubscription = CompletionList.ListBox
                    .GetPropertyChangedObservable(ItemsControl.ItemCountProperty)
                    .Subscribe(_ => UpdateEmptyState());
            }
            UpdateEmptyState();
        }

        private void OnWindowOpened(object? sender, EventArgs e)
        {
            UpdateEmptyState();
            if (!IsEmpty)
            {
                SelectFirstItem();
            }
        }

        private void OnTextAreaTextInput(object? sender, TextInputEventArgs e)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (IsOpen)
                {
                    UpdateEmptyState();
                    if (!IsEmpty && CompletionList.SelectedItem == null)
                    {
                        SelectFirstItem();
                    }
                }
            }, Avalonia.Threading.DispatcherPriority.Input);
        }

        public void UpdateEmptyState()
        {
            CompletionList.Classes.Set("empty", IsEmpty);
        }

        public void SelectFirstItem()
        {
            if (IsEmpty) return;

            var first = CompletionList.CurrentList?.FirstOrDefault()
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
            CompletionList.TemplateApplied -= OnCompletionListTemplateApplied;
            _itemCountSubscription?.Dispose();
            _itemCountSubscription = null;
            base.DetachEvents();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (IsEmpty)
            {
                if (e.Key is Key.Enter or Key.Return or Key.Tab or Key.Escape)
                {
                    Close();
                    if (e.Key is Key.Enter or Key.Return or Key.Tab)
                    {
                        return;
                    }
                    e.Handled = true;
                    return;
                }
            }

            base.OnKeyDown(e);
            UpdateEmptyState();
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

