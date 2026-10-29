using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quartz.Core.Enums;
using Quartz.Core.Models.BoardEntities;

namespace Quartz.UI.ViewModels;

public partial class CanvasVM : FileVM
{
    public CanvasVM(File2D owner) : base(owner.MainVM, filePath: owner.FilePath, parent: null)
    {
        Owner = owner;
        IsModified = owner.IsModified;
        owner.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(IsModified))
                IsModified = owner.IsModified;
            if (e.PropertyName == nameof(HasErrors))
                OnPropertyChanged(nameof(HasErrors));
            if (e.PropertyName == nameof(HasWarnings))
                OnPropertyChanged(nameof(HasWarnings));
        };
        if (owner is LayerVM layerVM)
        {
            if (layerVM.LayerModel != null)
            {
                Unit = layerVM.LayerModel.Unit;
            }
            if (layerVM.CachedPrimitives != null)
            {
                RenderData = layerVM.CachedPrimitives;
            }
        }
    }

    public File2D Owner { get; set; }

    public override Extension Extension => Owner.Extension;
    public override bool HasErrors => Owner?.HasErrors ?? false;
    public override bool HasWarnings => Owner?.HasWarnings ?? false;

    [ObservableProperty] private IReadOnlyList<DrawingPrimitive>? _renderData;
    [ObservableProperty] private LengthUnit _unit = LengthUnit.Mm;

    [ObservableProperty] private float _zoom = 1.0f;
    [ObservableProperty] private float _offsetX;
    [ObservableProperty] private float _offsetY;

    [ObservableProperty] private bool _isInitiallyCentered;
    [ObservableProperty] private bool _isOriginOffScreen;

    public Action? RequestCenterOrigin { get; set; }

    [RelayCommand]
    private void CenterOrigin()
    {
        RequestCenterOrigin?.Invoke();
    }

    public override void RemoveTab()
    {
        base.RemoveTab();
        Owner.Canvas = null;
    }
}