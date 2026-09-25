using System;
using Quartz.Application.Interfaces;
using Quartz.Core.Enums;
using Quartz.Core.Models;

namespace Quartz.UI.ViewModels;

public class StyleVM : File2D
{
    private readonly IBoardProcessingCoordinator _coordinator;

    public StyleVM(MainVM mainVM, string filePath, DirectoryVM? parent, IBoardProcessingCoordinator coordinator)
        : base(mainVM, filePath, parent, coordinator)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public override Extension Extension => Extension.Stly;

    public LayerModel? LayerModel { get; set; }

    protected override void ProcessDocument(string text)
    {
        Errors.Clear();

        // Если есть проект платы — запускаем пересборку ВСЕГО проекта через MainVM
        if (MainVM.CurrentBoard != null)
        {
            MainVM.ReloadCurrentProject();
            return;
        }

        // Одиночный файл
        var result = _coordinator.Process(text, null);

        // Ошибки заносим ВСЕГДА, чтобы редактор подсвечивал строки
        foreach (var err in result.Errors)
        {
            Errors.Add(err);
        }

        // Обновляем модель и канвас в любом случае, если парсер хоть что-то вернул!
        if (result.Model == null) 
            return;
        
        LayerModel = result.Model;

        if (Canvas == null)
            return;
            
        Canvas.RenderData = result.Primitives;
    }
}