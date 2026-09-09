using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Quartz.Core.Enums;
using Quartz.UI.Enums;

namespace Quartz.UI.ViewModels;

public abstract partial class FileVM : FileStructVM
{
    protected FileVM(MainVM mainVM, string filePath, DirectoryVM? parent) : base(mainVM, filePath, parent)
    {
    }

    public abstract Extension Extension { get; }

    public Side InsertSide
    {
        get;
        set => SetProperty(ref field, value);
    }

    public PanelVM? Panel { get; set; }

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