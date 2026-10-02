using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Quartz.Core.Models;

namespace Quartz.UI.ViewModels;

public partial class FileErrorGroupVM : ObservableObject
{
    public string FilePath { get; }
    public string FileName => Path.GetFileName(FilePath);

    public ObservableCollection<EditorError> Errors { get; } = [];

    public FileErrorGroupVM(string filePath, IEnumerable<EditorError> errors)
    {
        FilePath = filePath;
        foreach (var err in errors)
        {
            Errors.Add(err);
        }
    }
}