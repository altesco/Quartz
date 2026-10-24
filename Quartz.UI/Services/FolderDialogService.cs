using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Quartz.UI.Services;

public class FolderDialogService : IFolderDialogService
{
    public async Task<string?> OpenFolderAsync(string title = "Выберите папку проекта")
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.StorageProvider is not { } storageProvider)
        {
            return null;
        }

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        if (folders.Count == 0)
        {
            return null;
        }

        return folders[0].TryGetLocalPath();
    }

    public async Task<string?> OpenFileAsync(string title = "Выберите файл")
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.StorageProvider is not { } storageProvider)
        {
            return null;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        if (files.Count == 0)
        {
            return null;
        }

        return files[0].TryGetLocalPath();
    }
}
