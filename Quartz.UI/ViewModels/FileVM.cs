using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Quartz.Core.Enums;
using Quartz.Core.Models;
using Quartz.UI.Enums;

namespace Quartz.UI.ViewModels;

public abstract partial class FileVM : FileStructVM
{
    protected FileVM(MainVM mainVM, string filePath, DirectoryVM? parent) : base(mainVM, filePath, parent)
    {
    }

    public abstract Extension Extension { get; }

    public ObservableCollection<EditorError> Errors { get; } = [];

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

    [RelayCommand]
    private void ResetInsertSide() => InsertSide = Side.None;

    [RelayCommand]
    protected virtual void RemoveTab()
    {
        if (Panel is null) return;

        Panel.Tabs.Remove(this);
        if (Panel.Tabs.Count == 0)
            MainVM.RemoveEmptyPanel(Panel);
        else if (Panel.SelectedTab == this)
            Panel.SelectedTab = Panel.Tabs.Last();

        Panel = null;
    }
}