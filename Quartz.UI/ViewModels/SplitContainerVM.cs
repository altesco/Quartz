using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Quartz.UI.ViewModels;

public partial class SplitContainerVM : LayoutRootVM
{
    [ObservableProperty] private bool _isHorizontal;

    public ObservableCollection<LayoutRootVM> Children { get; } = new();
}