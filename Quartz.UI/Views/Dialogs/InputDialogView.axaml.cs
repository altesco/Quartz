using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Quartz.UI.ViewModels;

namespace Quartz.UI.Views.Dialogs;

public partial class InputDialogView : UserControl
{
    private Key? _heldKey;
    private int _keyDownCount;

    public InputDialogView()
    {
        InitializeComponent();

        var textBox = this.FindControl<TextBox>("InputTextBox");
        if (textBox != null)
        {
            textBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    if (DataContext is InputDialogVM vm)
                    {
                        vm.Confirm();
                        e.Handled = true;
                    }
                    return;
                }

                if (e.Key == _heldKey)
                {
                    _keyDownCount++;
                }
                else
                {
                    _heldKey = e.Key;
                    _keyDownCount = 1;
                }

                // Повтор ввода символов при долгом зажатии клавиши (на Linux Wayland/X11
                // Avalonia отправляет повторные KeyDown, но не генерирует повторные TextInput)
                if (_keyDownCount > 1
                    && !e.KeyModifiers.HasFlag(KeyModifiers.Control)
                    && !e.KeyModifiers.HasFlag(KeyModifiers.Alt)
                    && !string.IsNullOrEmpty(e.KeySymbol)
                    && e.KeySymbol.Length == 1
                    && !char.IsControl(e.KeySymbol[0]))
                {
                    var text = textBox.Text ?? string.Empty;
                    var start = Math.Min(textBox.SelectionStart, textBox.SelectionEnd);
                    var end = Math.Max(textBox.SelectionStart, textBox.SelectionEnd);
                    var newText = text.Remove(start, end - start).Insert(start, e.KeySymbol);
                    textBox.Text = newText;
                    textBox.CaretIndex = start + e.KeySymbol.Length;
                    e.Handled = true;
                }
            };

            textBox.KeyUp += (s, e) =>
            {
                if (e.Key == _heldKey)
                {
                    _heldKey = null;
                    _keyDownCount = 0;
                }
            };

            textBox.LostFocus += (s, e) =>
            {
                _heldKey = null;
                _keyDownCount = 0;
            };
        }

        Loaded += (s, e) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (textBox != null)
                {
                    textBox.Focus();
                    if (!string.IsNullOrEmpty(textBox.Text))
                    {
                        // If there is an extension, select only the name without extension
                        var nameWithoutExt = Path.GetFileNameWithoutExtension(textBox.Text);
                        if (!string.IsNullOrEmpty(nameWithoutExt) && nameWithoutExt != textBox.Text)
                        {
                            textBox.SelectionStart = 0;
                            textBox.SelectionEnd = nameWithoutExt.Length;
                        }
                        else
                        {
                            textBox.SelectAll();
                        }
                    }
                }
            }, DispatcherPriority.Input);
        };
    }
}
