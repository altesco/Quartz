using System.Threading.Tasks;
using Quartz.Application.Interfaces;
using Quartz.Core.Enums;

namespace Quartz.UI.ViewModels;

public sealed partial class PlainFileVM : EditorVM
{
    public PlainFileVM(
        MainVM mainVM,
        string filePath,
        DirectoryVM? parent,
        IBoardProcessingCoordinator? coordinator = null)
        : base(mainVM, filePath, parent, coordinator)
    {
    }

    public override Extension Extension => Extension.Other;

    protected override Task ProcessDocument(string text)
    {
        return Task.CompletedTask;
    }
}

