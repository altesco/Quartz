using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Quartz.Application.Interfaces;
using Quartz.Core.Enums;
using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities.Styles;

namespace Quartz.UI.ViewModels;

public class StylesFileVM : EditorVM
{
    private readonly IBoardProcessingCoordinator _coordinator;

    public StylesFileVM(MainVM mainVM, string filePath, DirectoryVM? parent, IBoardProcessingCoordinator coordinator)
        : base(mainVM, filePath, parent, coordinator)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public override Extension Extension => Extension.Stly;

    public List<Style>? Styles { get; set; }

    protected override async Task ProcessDocument(string text)
    {
        // Если открыта плата — пинаем весь проект
        if (MainVM.CurrentBoard != null)
        {
            MainVM.ReloadCurrentProject();
            return;
        }

        // Одиночный режим (работаем только со стилями без контекста платы)
        var result = _coordinator.ProcessStyles(text);
        ApplySingleResult(result);
    }

    /// <summary>
    /// Применяет результат распаршенных стилей при глобальном пересчете проекта
    /// </summary>
    public void ApplyProjectResult(ProcessResult<List<Style>> stylesResult)
    {
        Errors.Clear();
        foreach (var err in stylesResult.Errors)
        {
            Errors.Add(err);
        }

        if (stylesResult.Model != null)
        {
            Styles = stylesResult.Model;
        }
    }

    private void ApplySingleResult(ProcessResult<List<Style>> result)
    {
        Errors.Clear();
        foreach (var err in result.Errors)
        {
            Errors.Add(err);
        }

        if (result.Model != null)
        {
            Styles = result.Model;
        }
    }
}