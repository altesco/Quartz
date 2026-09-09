using System;
using System.Collections.ObjectModel;
using System.Timers;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using Quartz.Application.Interfaces;
using Quartz.Core.Models;

namespace Quartz.UI.ViewModels;

public abstract partial class EditorVM : FileVM
{
    private readonly IBoardProcessingCoordinator _coordinator;

    private const int Delay = 300;
    private readonly Timer _timer;

    [ObservableProperty] private TextDocument _document = new();

    public ObservableCollection<EditorError> Errors { get; } = [];

    [ObservableProperty] private double _scrollX;
    [ObservableProperty] private double _scrollY;

    public EditorVM(MainVM mainVM, string filePath, DirectoryVM? parent, IBoardProcessingCoordinator coordinator)
        : base(mainVM, filePath, parent)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));

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
            oldValue.TextChanged -= DocumentOnTextChanged;

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
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            Errors.Clear();

            if (Document == null)
                return;

            var text = Document.Text;

            if (string.IsNullOrEmpty(text))
                return;

            // Вся обработка выполняется в одном вызове!
            var result = _coordinator.Process(text);

            foreach (var err in result.Errors)
            {
                Errors.Add(err);
            }

            OnProcessingFinished(result);
        });
    }

    protected virtual void OnProcessingFinished(LayerProcessResult result)
    {
        // Переопределяется в наследниках для обновления Canvas и моделей
    }
}