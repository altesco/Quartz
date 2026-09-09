using CommunityToolkit.Mvvm.ComponentModel;

namespace Quartz.UI.ViewModels;

public abstract partial class LayoutRootVM : ObservableObject
{
    [ObservableProperty] private string _size = "1*";
}