using Microsoft.ClearScript.V8;
using Quartz.Core.Interfaces;
using Quartz.Core.Models;

namespace Quartz.Infrastructure.Parsers;

public class ClearScriptYamlParser : IYamlParser, IDisposable
{
    private readonly V8ScriptEngine _engine;

    public ClearScriptYamlParser()
    {
        _engine = new V8ScriptEngine();

        var libPath = Path.Combine(AppContext.BaseDirectory, "Resources", "yaml.js");

        if (!File.Exists(libPath))
        {
            throw new FileNotFoundException($"Не найден файл бандла yaml.js по пути: {libPath}");
        }

        _engine.Execute(@"
            if (typeof globalThis.window === 'undefined') {
                globalThis.window = globalThis;
            }
        ");

        _engine.Execute(File.ReadAllText(libPath));

        // Вся логика сведена ТОЛЬКО к валидации синтаксиса YAML через parseDocument
        _engine.Execute(@"
            function validateYaml(text) {
                const errors = [];
                if (!text) return errors;

                try {
                    const yamlLib = globalThis.YAML || (typeof YAML !== 'undefined' ? YAML : null);

                    if (!yamlLib || typeof yamlLib.parseDocument !== 'function') {
                        return [{
                            message: 'Ошибка инициализации: объект YAML не найден в V8 context',
                            line: 1, column: 1, length: 1
                        }];
                    }

                    const doc = yamlLib.parseDocument(text, { prettyErrors: true });
                    const syntaxErrors = doc.errors || [];

                    for (let i = 0; i < syntaxErrors.length; i++) {
                        const err = syntaxErrors[i];
                        let line = 1, column = 1, length = 1;

                        if (err.linePos && err.linePos.length > 0) {
                            line = err.linePos[0].line;
                            column = err.linePos[0].col;

                            if (err.linePos.length > 1 && err.linePos[1].line === line) {
                                length = Math.max(1, err.linePos[1].col - column);
                            }
                        }

                        if (err.pos && err.pos.length >= 2) {
                            const diff = err.pos[1] - err.pos[0];
                            if (diff > 0) length = diff;
                        } else if (err.range && err.range.length >= 2) {
                            const diffRange = err.range[1] - err.range[0];
                            if (diffRange > 0) length = diffRange;
                        }

                        errors.push({
                            message: err.message,
                            line: line,
                            column: column,
                            length: Math.max(1, length)
                        });
                    }
                } catch (e) {
                    errors.push({
                        message: e.message || String(e),
                        line: 1, column: 1, length: 1
                    });
                }

                return errors;
            }
        ");
    }

    public IReadOnlyList<EditorError> ValidateSyntax(string? yamlContent)
    {
        var result = new List<EditorError>();
        dynamic jsErrors = _engine.Script.validateYaml(yamlContent ?? string.Empty);
        int length = jsErrors.length;

        for (int i = 0; i < length; i++)
        {
            dynamic err = jsErrors[i];
            result.Add(new EditorError
            {
                Message = (string)err.message,
                Line = (int)err.line,
                Column = (int)err.column,
                Length = err.length != null ? Math.Max(1, (int)err.length) : 1
            });
        }

        return result;
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}