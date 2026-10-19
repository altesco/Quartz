using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Timers;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quartz.Application.Interfaces;
using Quartz.Core.Models;

namespace Quartz.UI.ViewModels;

public abstract partial class EditorVM : FileVM
{

    private const int Delay = 300;
    private readonly Timer _timer;
    private bool _isLoaded;

    [ObservableProperty] private TextDocument? _document = new();

    [ObservableProperty] private double _scrollX;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTitleBarShadow))]
    private double _scrollY;

    public bool HasTitleBarShadow => ScrollY > 1;

    private double _fontSize = 14;

    public double FontSize
    {
        get => _fontSize;
        set => SetProperty(ref _fontSize, value);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CaretPositionText))]
    private int _caretLine = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CaretPositionText))]
    private int _caretColumn = 1;

    public string CaretPositionText => $"Строка {CaretLine}, столбец {CaretColumn}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IndentationText))]
    private int _indentationSize = 2;

    public string IndentationText => $"Пробелов: {IndentationSize}";

    public event Action<long, long>? NavigationRequested;
    private (long Line, long Column)? _pendingNavigation;

    public void RequestNavigateTo(long line, long column)
    {
        _pendingNavigation = (line, column);
        NavigationRequested?.Invoke(line, column);
    }

    public (long Line, long Column)? ConsumePendingNavigation()
    {
        var pending = _pendingNavigation;
        _pendingNavigation = null;
        return pending;
    }

    public EditorVM(MainVM mainVM, string filePath, DirectoryVM? parent, IBoardProcessingCoordinator? coordinator = null)
        : base(mainVM, filePath, parent)
    {
        if (Document != null)
        {
            Document.TextChanged += DocumentOnTextChanged;
            Document.UndoStack.PropertyChanged += UndoStackOnPropertyChanged;
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
            oldValue.UndoStack.PropertyChanged -= UndoStackOnPropertyChanged;
        }

        if (newValue != null)
        {
            newValue.TextChanged += DocumentOnTextChanged;
            newValue.UndoStack.PropertyChanged += UndoStackOnPropertyChanged;
            IsModified = !newValue.UndoStack.IsOriginalFile;
        }
        else
        {
            IsModified = false;
        }
    }

    private void UndoStackOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UndoStack.IsOriginalFile) && Document != null)
        {
            IsModified = !Document.UndoStack.IsOriginalFile;
        }
    }

    private void DocumentOnTextChanged(object? sender, EventArgs e)
    {
        _timer.Stop();
        _timer.Start();

        if (Document != null)
        {
            IsModified = !Document.UndoStack.IsOriginalFile;
        }
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

    private bool _isOversized;

    /// <summary>
    /// Загружает содержимое файла с диска, если документ еще не был инициализирован.
    /// </summary>
    public void LoadDocumentIfEmpty()
    {
        if (_isLoaded) return;
        _isLoaded = true;

        if (File.Exists(FilePath))
        {
            try
            {
                var fileInfo = new FileInfo(FilePath);
                const long maxFileSize = 4 * 1024 * 1024; // 4 MB

                if (fileInfo.Length > maxFileSize)
                {
                    _isOversized = true;
                    var sizeMb = fileInfo.Length / (1024.0 * 1024.0);
                    var warning = $"// [Файл слишком большой: {sizeMb:F2} МБ]\n\n" +
                                  $"// Отображение файлов более {maxFileSize / (1024 * 1024)} МБ ограничено во избежание зависания интерфейса.";
                    if (Document != null)
                    {
                        Document.Text = warning;
                        Document.UndoStack.ClearAll();
                    }
                    else
                    {
                        Document = new TextDocument(warning);
                    }
                    return;
                }

                var text = File.ReadAllText(FilePath);
                if (Document != null)
                {
                    Document.Text = text;
                    Document.UndoStack.ClearAll();
                    Document.UndoStack.MarkAsOriginalFile();
                }
                else
                {
                    Document = new TextDocument(text);
                    Document.UndoStack.MarkAsOriginalFile();
                }
                IsModified = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load file {FilePath}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Сохраняет содержимое редактора обратно в файл на диске.
    /// </summary>
    [RelayCommand]
    public void Save()
    {
        if (_isOversized || Document == null || string.IsNullOrEmpty(FilePath))
            return;

        try
        {
            File.WriteAllText(FilePath, Document.Text);
            Document.UndoStack.MarkAsOriginalFile();
            IsModified = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save {FilePath}: {ex.Message}");
        }
    }
}