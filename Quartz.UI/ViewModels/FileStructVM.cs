using System;
using System.IO;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Quartz.UI.ViewModels;

public abstract partial class FileStructVM : ObservableObject
{
    protected FileStructVM(MainVM mainVM, string filePath, DirectoryVM? parent)
    {
        MainVM = mainVM;
        FilePath = filePath;
        Parent = parent;
    }

    public MainVM MainVM { get; set; }

    public string FilePath
    {
        get; 
        set
        {
            SetProperty(ref field, value);
            OnPropertyChanged(nameof(Header));
        }
    }

    public string Header => Path.GetFileName(FilePath);
    public virtual bool IsDirectory => false;
    public virtual bool HasErrors => false;
    public virtual bool HasWarnings => false;
    public DirectoryVM? Parent { get; set; }

    [ObservableProperty]
    private bool _isExpanded;

    partial void OnIsExpandedChanged(bool value)
    {
        MainVM?.NotifyFolderExpandedChanged();
    }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isCut;

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
        {
            MainVM.SelectedFileStruct = this;
        }
        else if (MainVM.SelectedFileStruct == this)
        {
            MainVM.SelectedFileStruct = null;
        }
    }

    [RelayCommand]
    public void OpenFile()
    {
        if (this is FileVM file)
        {
            MainVM.OpenFileTab(file);
        }
        else if (this is DirectoryVM)
        {
            IsExpanded = !IsExpanded;
        }
    }

    [RelayCommand]
    public void CreateStylesFile()
    {
        var dir = IsDirectory ? FilePath : (Parent?.FilePath ?? MainVM.ProjectRootDir ?? string.Empty);
        MainVM.PromptCreateFileInDirectory(dir, ".stly", "Создать файл стилей", "styles: []\n");
    }

    [RelayCommand]
    public void CreateBoardFile()
    {
        var dir = IsDirectory ? FilePath : (Parent?.FilePath ?? MainVM.ProjectRootDir ?? string.Empty);
        MainVM.PromptCreateFileInDirectory(dir, ".pcby", "Создать плату", "layers: []\nimports: []\nnets: []\nstyles: []\n");
    }

    [RelayCommand]
    public void CreateLayerFile()
    {
        var dir = IsDirectory ? FilePath : (Parent?.FilePath ?? MainVM.ProjectRootDir ?? string.Empty);
        MainVM.PromptCreateFileInDirectory(dir, ".layy", "Создать слой", "name: layer\nunit: mm\nshape: !rect\n  width: 100\n  height: 100\n\ncomponents: []\n");
    }

    [RelayCommand]
    public void CreateGeneralFile()
    {
        var dir = IsDirectory ? FilePath : (Parent?.FilePath ?? MainVM.ProjectRootDir ?? string.Empty);
        MainVM.PromptCreateFileInDirectory(dir, null, "Создать файл", "");
    }

    [RelayCommand]
    public void CreateTextFile() => CreateGeneralFile();

    [RelayCommand]
    public void OpenFileToSide()
    {
        if (this is FileVM file)
        {
            MainVM.OpenFileToSide(file);
        }
    }

    [RelayCommand]
    public void RevealInFileManager()
    {
        MainVM.RevealInFileManager(FilePath);
    }

    [RelayCommand]
    public void Cut()
    {
        MainVM.Cut(this);
    }

    [RelayCommand]
    public void Copy()
    {
        MainVM.Copy(this);
    }

    [RelayCommand]
    public void Paste()
    {
        if (this is DirectoryVM dir)
        {
            MainVM.Paste(dir);
        }
        else if (Parent != null)
        {
            MainVM.Paste(Parent);
        }
        else if (MainVM.ProjectRootDirectory != null)
        {
            MainVM.Paste(MainVM.ProjectRootDirectory);
        }
    }

    [RelayCommand]
    public async System.Threading.Tasks.Task CopyPath()
    {
        await MainVM.SetClipboardTextAsync(FilePath);
    }

    [RelayCommand]
    public async System.Threading.Tasks.Task CopyRelativePath()
    {
        string relPath = MainVM.GetRelativePath(FilePath);
        await MainVM.SetClipboardTextAsync(relPath);
    }

    [RelayCommand]
    public void Rename()
    {
        MainVM.PromptRename(this);
    }

    [RelayCommand]
    public void Delete()
    {
        MainVM.PromptDelete(this);
    }

    [RelayCommand]
    protected void TapStart(PointerPressedEventArgs? e)
    {
        if (e is null)
            return;

        MainVM.TappedFile = this;
        MainVM.DragStartX = e.GetPosition(null).X;
        MainVM.DragStartY = e.GetPosition(null).Y;
    }

    [RelayCommand]
    protected void TapEnd()
    {
        if (MainVM.TappedFile == this)
        {
            if (MainVM.DraggedFile != null)
            {
                MainVM.ResetDragStatus();
                return;
            }

            switch (this)
            {
                case DirectoryVM:
                    break;
                case FileVM file:
                    MainVM.OpenFileTab(file);
                    break;
            }
        }
        else
        {
            if (this is not DirectoryVM targetDir || MainVM.DraggedFile == null)
            {
                MainVM.ResetDragStatus();
                return;
            }

            var draggedFile = MainVM.DraggedFile;
            if (draggedFile.Parent is null)
            {
                MainVM.ResetDragStatus();
                return;
            }

            var oldPath = draggedFile.FilePath;
            var newPath = Path.Combine(FilePath, draggedFile.Header);

            if (oldPath == newPath)
            {
                MainVM.ResetDragStatus();
                return;
            }

            switch (draggedFile)
            {
                case FileVM file:
                    try
                    {
                        if (File.Exists(newPath) || Directory.Exists(newPath))
                            throw new IOException($"Цель уже существует: {newPath}");

                        File.Move(oldPath, newPath);
                        file.FilePath = newPath;
                    }
                    catch (Exception)
                    {
                        //ShowFileMoveError("переместить файл", oldPath, newPath, ex);
                        MainVM.ResetDragStatus();
                        return;
                    }
                    break;

                case DirectoryVM sourceDir:
                    if (!ParentCheckRecursive(targetDir.Parent, sourceDir))
                    {
                        MainVM.ResetDragStatus();
                        return;
                    }

                    try
                    {
                        if (File.Exists(newPath) || Directory.Exists(newPath))
                            throw new IOException($"Цель уже существует: {newPath}");

                        Directory.Move(oldPath, newPath);
                        sourceDir.UpdatePathRecursive(oldPath, newPath);
                    }
                    catch (Exception)
                    {
                        //ShowFileMoveError("переместить папку", oldPath, newPath, ex);
                        MainVM.ResetDragStatus();
                        return;
                    }
                    break;
            }

            draggedFile.Parent.Children.Remove(draggedFile);
            draggedFile.Parent = targetDir;
            targetDir.Children.Add(draggedFile);

            int currentIndex = targetDir.Children.Count - 1;
            while (currentIndex > 0)
            {
                var prev = targetDir.Children[currentIndex - 1];

                if (draggedFile is FileVM && prev is DirectoryVM)
                    break;

                if ((draggedFile is DirectoryVM && prev is DirectoryVM ||
                     draggedFile is FileVM && prev is FileVM) &&
                    string.Compare(
                        draggedFile.Header,
                        prev.Header,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                    break;

                targetDir.Children.Move(oldIndex: currentIndex, newIndex: currentIndex - 1);
                currentIndex--;
            }

            MainVM.UpdateBreadcrumbs();
        }

        MainVM.ResetDragStatus();
    }

    private bool ParentCheckRecursive(DirectoryVM? parent, DirectoryVM draggedDir)
    {
        if (parent is null)
            return true;
        if (parent == draggedDir)
            return false;
        return ParentCheckRecursive(parent.Parent, draggedDir);
    }
}