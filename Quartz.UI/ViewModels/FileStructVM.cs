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
    public DirectoryVM? Parent { get; set; }

    [RelayCommand]
    protected void TapStart(PointerPressedEventArgs? e)
    {
        if (e is null)
            return;

        MainVM.TappedFile = this;
        MainVM.DragStartX = e.GetPosition(null).X;
        MainVM.DragStartY = e.GetPosition(null).Y;

        e.Pointer.Capture(null);
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
                    if (file.Panel != null)
                    {
                        MainVM.ActivePanel = file.Panel;
                        file.Panel.SelectedTab = file;
                    }
                    else
                    {
                        MainVM.ActivePanel.Tabs.Add(file);
                        MainVM.ActivePanel.SelectedTab = file;
                        file.Panel = MainVM.ActivePanel;
                    }
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
                    catch (Exception ex)
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
                    catch (Exception ex)
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