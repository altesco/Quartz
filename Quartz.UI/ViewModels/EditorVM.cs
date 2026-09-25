using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Timers;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using Quartz.Application.Interfaces;
using Quartz.Core.Models;

namespace Quartz.UI.ViewModels;

public abstract partial class EditorVM : FileVM
{

    private const int Delay = 300;
    private readonly Timer _timer;

    [ObservableProperty] private TextDocument? _document = new();

    public ObservableCollection<EditorError> Errors { get; } = [];

    [ObservableProperty] private double _scrollX;
    [ObservableProperty] private double _scrollY;
    [ObservableProperty] private double _fontSize = 14;

    public EditorVM(MainVM mainVM, string filePath, DirectoryVM? parent, IBoardProcessingCoordinator coordinator)
        : base(mainVM, filePath, parent)
    {
        if (Document != null)
        {
            Document.TextChanged += DocumentOnTextChanged;
        }

        _timer = new Timer(Delay)
        {
            AutoReset = false
        };

        _timer.Elapsed += OnTimerElapsed;
    }

    partial void OnDocumentChanged(TextDocument? oldValue, TextDocument? newValue)
    {
        if (oldValue != null)
        {
            oldValue.TextChanged -= DocumentOnTextChanged;
        }

        if (newValue != null)
            newValue.TextChanged += DocumentOnTextChanged;
    }

    private void DocumentOnTextChanged(object? sender, EventArgs e)
    {
        _timer.Stop();
        _timer.Start();
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(TriggerProcess);
    }

    /// <summary>
    /// Принудительный запуск обработки документа в обход таймера (например, при обновлении других файлов)
    /// </summary>
    public void TriggerProcess()
    {
        _timer.Stop(); // Сбрасываем таймер, если он тикал
        Errors.Clear();

        if (Document == null)
            return;

        var text = Document.Text;
        if (string.IsNullOrEmpty(text))
            return;

        _ = ProcessDocument(text);
    }

    protected abstract Task ProcessDocument(string text);

    /// <summary>
    /// Возвращает измененный текст из открытого документа или считывает с диска
    /// </summary>
    public string GetActualText()
    {
        if (Document != null && !string.IsNullOrEmpty(Document.Text))
            return Document.Text;

        try
        {
            return File.ReadAllText(FilePath);
        }
        catch
        {
            return string.Empty;
        }
    }
}