using CommunityToolkit.Mvvm.Input;
using Quartz.Application.Interfaces;

namespace Quartz.UI.ViewModels;

public abstract partial class File2D : EditorVM
{
    protected File2D(MainVM mainVM, string filePath, DirectoryVM? parent, IBoardProcessingCoordinator coordinator)
        : base(mainVM, filePath, parent, coordinator)
    {
    }

    public CanvasVM? Canvas { get; set; }

    public override bool CanOpenCanvas => this is LayerVM;

    public override void OpenCanvas()
    {
        if (Canvas != null && Panel != null && Panel.Tabs.Contains(Canvas))
        {
            Panel.SelectedTab = Canvas;
            return;
        }

        LoadDocumentIfEmpty();

        Canvas = new CanvasVM(owner: this)
        {
            Panel = Panel
        };

        if (this is LayerVM layerVM && layerVM.CachedPrimitives == null)
        {
            layerVM.TriggerProcess();
        }

        Panel?.Tabs.Add(Canvas);
        if (Panel != null)
        {
            Panel.SelectedTab = Canvas;
        }
    }
}