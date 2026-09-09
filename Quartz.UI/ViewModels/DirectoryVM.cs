using System.Collections.ObjectModel;
using System.IO;

namespace Quartz.UI.ViewModels;

public class DirectoryVM : FileStructVM
{
    public DirectoryVM(MainVM mainVM, string filePath, DirectoryVM? parent) : base(mainVM, filePath, parent)
    {
    }

    public ObservableCollection<FileStructVM> Children { get; } = [];

    public void UpdatePathRecursive(string oldParentPath, string newParentPath)
    {
        FilePath = BuildNewPath(FilePath, oldParentPath, newParentPath);

        foreach (var child in Children)
        {
            if (child is FileVM file)
                file.FilePath = BuildNewPath(child.FilePath, oldParentPath, newParentPath);
            else if (child is DirectoryVM dir)
                dir.UpdatePathRecursive(oldParentPath, newParentPath);
        }
    }

    private static string BuildNewPath(string currentPath, string oldParentPath, string newParentPath)
    {
        var relativePath = Path.GetRelativePath(oldParentPath, currentPath);

        if (relativePath == "." || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}") || relativePath == "..")
            return currentPath;

        return Path.GetFullPath(Path.Combine(newParentPath, relativePath));
    }
}