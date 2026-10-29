using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quartz.Core.Enums;
using Quartz.Core.Models;
using Quartz.UI.Enums;

namespace Quartz.UI.ViewModels;

public abstract partial class FileVM : FileStructVM
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPinnedAndModified))]
    [NotifyPropertyChangedFor(nameof(IsUnpinnedAndModified))]
    private bool _isPinned;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPinnedAndModified))]
    [NotifyPropertyChangedFor(nameof(IsUnpinnedAndModified))]
    private bool _isModified;

    public bool IsPinnedAndModified => IsPinned && IsModified;
    public bool IsUnpinnedAndModified => !IsPinned && IsModified;

    protected FileVM(MainVM mainVM, string filePath, DirectoryVM? parent) : base(mainVM, filePath, parent)
    {
        Errors.CollectionChanged += (s, e) => SyncProblems();
        Warnings.CollectionChanged += (s, e) => SyncProblems();
    }

    public abstract Extension Extension { get; }

    public ObservableCollection<EditorError> Errors { get; } = [];
    public ObservableCollection<EditorWarning> Warnings { get; } = [];
    public ObservableCollection<Problem> Problems { get; } = [];

    public override bool HasErrors => Errors.Count > 0;
    public override bool HasWarnings => !HasErrors && Warnings.Count > 0;
    public int TotalIssueCount => Errors.Count + Warnings.Count;

    private void SyncProblems()
    {
        Problems.Clear();
        foreach (var err in Errors) Problems.Add(err);
        foreach (var warn in Warnings) Problems.Add(warn);

        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(HasWarnings));
        OnPropertyChanged(nameof(TotalIssueCount));
    }

    public void NotifyErrorStatusChanged()
    {
        SyncProblems();
    }

    public Side InsertSide
    {
        get;
        set => SetProperty(ref field, value);
    }

    public PanelVM? Panel { get; set; }

    public ObservableCollection<DirectoryVM> BreadcrumbFolders { get; } = [];

    public void UpdateBreadcrumbs()
    {
        BreadcrumbFolders.Clear();
        var folders = new List<DirectoryVM>();
        var current = Parent;
        var rootDir = MainVM.ProjectRootDir;

        while (current != null)
        {
            if (current.Parent == null || (!string.IsNullOrEmpty(rootDir) && string.Equals(current.FilePath, rootDir, StringComparison.OrdinalIgnoreCase)))
                break;

            folders.Add(current);
            current = current.Parent;
        }

        folders.Reverse();
        foreach (var folder in folders)
        {
            BreadcrumbFolders.Add(folder);
        }
    }

    public virtual bool CanOpenCanvas => false;

    [RelayCommand]
    public virtual void OpenCanvas() { }

    [RelayCommand]
    public void TogglePin()
    {
        IsPinned = !IsPinned;
        if (IsPinned && Panel != null)
        {
            var currentIndex = Panel.Tabs.IndexOf(this);
            if (currentIndex > 0)
            {
                Panel.Tabs.Move(currentIndex, 0);
            }
        }
    }

    [RelayCommand]
    private void TabActionButton()
    {
        if (IsPinned)
        {
            TogglePin();
        }
        else
        {
            RemoveTab();
        }
    }

    [RelayCommand]
    private void CloseOtherTabs()
    {
        if (Panel is null) return;

        var tabsToClose = Panel.Tabs
            .Where(t => t != this && !t.IsPinned)
            .ToList();

        foreach (var tab in tabsToClose)
        {
            tab.RemoveTab();
        }

        Panel.SelectedTab = this;
    }

    [RelayCommand]
    private void CloseSavedTabs()
    {
        if (Panel is null) return;

        var tabsToClose = Panel.Tabs
            .Where(t => !t.IsModified && !t.IsPinned)
            .ToList();

        foreach (var tab in tabsToClose)
        {
            tab.RemoveTab();
        }
    }

    [RelayCommand]
    private void CloseAllTabs()
    {
        if (Panel is null) return;

        var tabsToClose = Panel.Tabs
            .Where(t => !t.IsPinned)
            .ToList();

        foreach (var tab in tabsToClose)
        {
            tab.RemoveTab();
        }
    }

    private static async Task SetClipboardTextAsync(string text)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.Clipboard != null)
        {
            await desktop.MainWindow.Clipboard.SetTextAsync(text);
        }
    }

    [RelayCommand]
    public async Task CopyPathAsync()
    {
        if (!string.IsNullOrEmpty(FilePath))
        {
            await SetClipboardTextAsync(FilePath);
        }
    }

    [RelayCommand]
    public async Task CopyRelativePathAsync()
    {
        if (string.IsNullOrEmpty(FilePath)) return;

        var rootDir = MainVM?.ProjectRootDir;
        var relPath = !string.IsNullOrEmpty(rootDir)
            ? Path.GetRelativePath(rootDir, FilePath)
            : Header;

        await SetClipboardTextAsync(relPath);
    }

    [RelayCommand]
    public void ShowInExplorer()
    {
        if (MainVM == null) return;

        MainVM.ActiveSidebarTab = SidebarTab.Explorer;

        var cur = Parent;
        while (cur != null)
        {
            cur.IsExpanded = true;
            cur = cur.Parent;
        }

        IsSelected = true;
    }

    [RelayCommand]
    public void ShowInFileManager()
    {
        try
        {
            if (OperatingSystem.IsWindows() && File.Exists(FilePath))
            {
                Process.Start("explorer.exe", $"/select,\"{FilePath}\"");
                return;
            }

            var dir = File.Exists(FilePath)
                ? Path.GetDirectoryName(FilePath)
                : (Directory.Exists(FilePath) ? FilePath : null);

            if (string.IsNullOrEmpty(dir)) return;

            if (OperatingSystem.IsWindows())
            {
                Process.Start("explorer.exe", $"\"{dir}\"");
            }
            else if (OperatingSystem.IsMacOS())
            {
                if (File.Exists(FilePath))
                    Process.Start("open", $"-R \"{FilePath}\"");
                else
                    Process.Start("open", $"\"{dir}\"");
            }
            else // Linux
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = $"\"{dir}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true
                });
            }
        }
        catch
        {
        }
    }

    [RelayCommand]
    private void ResetInsertSide() => InsertSide = Side.None;

    [RelayCommand]
    public virtual void RemoveTab()
    {
        if (Panel is null) return;
        Panel.CloseTab(this);
    }
}