using System.Threading.Tasks;

namespace Quartz.UI.Services;

public interface IFolderDialogService
{
    Task<string?> OpenFolderAsync(string title = "Выберите папку проекта");
}

