using System;
using Quartz.Application.Interfaces;
using Quartz.Core.Enums;
using Quartz.Core.Models;

namespace Quartz.UI.ViewModels;

public class LayerVM : File2D
{
    private readonly IBoardProcessingCoordinator _coordinator;

    public LayerVM(MainVM mainVM, string filePath, DirectoryVM? parent, IBoardProcessingCoordinator coordinator)
        : base(mainVM, filePath, parent, coordinator)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public override Extension Extension => Extension.Lyr;

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

        // ЕСЛИ ЕСТЬ ОШИБКИ — ЗАМИРАЕМ! Ни модель, ни холст НЕ ТРОГАЕМ!
        if (result.Errors.Count > 0)
        {
            return;
        }

        // Обновляем только если ошибок нет вообще!
        if (result.Model is { } model)
        {
            LayerModel = model;
        }

        if (Canvas != null)
        {
            Canvas.RenderData = result.Primitives;
        }
    }
}