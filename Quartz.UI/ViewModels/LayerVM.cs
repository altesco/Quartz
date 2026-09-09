using Avalonia.Threading;
using Quartz.Application.Interfaces;
using Quartz.Core.Enums;
using Quartz.Core.Models;

namespace Quartz.UI.ViewModels;

public partial class LayerVM : File2D
{
    public LayerVM(MainVM mainVM, string filePath, DirectoryVM? parent, IBoardProcessingCoordinator coordinator)
        : base(mainVM, filePath, parent, coordinator)
    {
    }

    public override Extension Extension => Extension.Lyr;

    public LayerModel? LayerModel { get; set; }

    protected override void OnProcessingFinished(LayerProcessResult result)
    {
        if (result.Errors.Count == 0 && Canvas != null)
        {
            // Переключаемся на UI-поток Avalonia
            Dispatcher.UIThread.Post(() =>
            {
                LayerModel = result.Model;
                Canvas.RenderData = result.Primitives;
            });
        }
    }
}