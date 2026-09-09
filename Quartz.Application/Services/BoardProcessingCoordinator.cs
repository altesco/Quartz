using Quartz.Application.Interfaces;
using Quartz.Core.Interfaces;
using Quartz.Core.Models;

namespace Quartz.Application.Services;

public class BoardProcessingCoordinator : IBoardProcessingCoordinator
{
    private readonly IYamlParser _syntaxParser;
    private readonly IYamlSchemaValidator _schemaValidator;
    private readonly IYamlDomainParser _domainParser;
    private readonly ILogicValidationService _logicValidator;
    private readonly IDrawingGenerationService _drawingGenerator;

    public BoardProcessingCoordinator(
        IYamlParser syntaxParser,
        IYamlSchemaValidator schemaValidator,
        IYamlDomainParser domainParser,
        ILogicValidationService logicValidator,
        IDrawingGenerationService drawingGenerator)
    {
        _syntaxParser = syntaxParser;
        _schemaValidator = schemaValidator;
        _domainParser = domainParser;
        _logicValidator = logicValidator;
        _drawingGenerator = drawingGenerator;
    }

    public LayerProcessResult Process(string text)
    {
        var allErrors = new List<EditorError>();

        // ЭТАП 1: Синтаксис V8 (скобки, отступы, двоеточия)
        var syntaxErrors = _syntaxParser.ValidateSyntax(text);
        if (syntaxErrors.Count > 0)
        {
            return new LayerProcessResult(syntaxErrors.ToList(), [], null);
        }

        // ЭТАП 2: Валидация нейминга и тегов (!resistor, кривые ключи)
        var schemaErrors = _schemaValidator.ValidateSchemaAndTags(text);
        allErrors.AddRange(schemaErrors);

        if (schemaErrors.Count > 0)
        {
            return new LayerProcessResult(allErrors, [], null);
        }

        // Десериализация в доменную модель
        var model = _domainParser.Parse(text, out var domainErrors);
        allErrors.AddRange(domainErrors);

        if (model == null || allErrors.Count > 0)
        {
            return new LayerProcessResult(allErrors, [], model);
        }

        // ЭТАП 3: Логическая валидация (дубликаты Name, висячие трассы)
        var logicErrors = _logicValidator.Validate(model);
        allErrors.AddRange(logicErrors);

        if (logicErrors.Count > 0)
        {
            return new LayerProcessResult(allErrors, [], model);
        }

        // ЭТАП 4: Генерация графики для Canvas
        //ResolveTraceCoordinates(model);
        var primitives = _drawingGenerator.Generate(model);

        return new LayerProcessResult([], primitives, model);
    }
}