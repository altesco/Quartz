using System;
using System.Collections.Generic;
using System.IO;
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

        // 1. Создаем минимальные глобальные заглушки и загружаем автономный бандл
        _engine.Execute(@"
            if (typeof globalThis.window === 'undefined') {
                globalThis.window = globalThis;
            }
        ");
        _engine.Execute(File.ReadAllText(libPath));

        // 2. Регистрируем JS-функцию валидации синтаксиса
        // В конструкторе ClearScriptYamlParser.cs заменяем JS-функцию:
        _engine.Execute(@"
            function validateYaml(text) {
                var errors = [];
                if (!text) return errors;

                try {
                    var yamlLib = globalThis.YAML || (typeof YAML !== 'undefined' ? YAML : null);
                    
                    if (!yamlLib || typeof yamlLib.parseDocument !== 'function') {
                        return [{ message: 'Ошибка инициализации: объект YAML не найден в V8 context', line: 1, column: 1, length: 1 }];
                    }

                    var doc = yamlLib.parseDocument(text);
                    var problems = [].concat(doc.errors || []);

                    for (var i = 0; i < problems.length; i++) {
                        var err = problems[i];
                        var line = 1;
                        var col = 1;
                        var len = 1;

                        // 1. Извлекаем начальные строку и колонку
                        if (err.linePos && err.linePos.length > 0) {
                            line = err.linePos[0].line;
                            col = err.linePos[0].col;

                            // Если есть конечные координаты на той же строке
                            if (err.linePos.length > 1 && err.linePos[1].line === line) {
                                len = Math.max(1, err.linePos[1].col - col);
                            }
                        }

                        // 2. Если длина не вычислена, пробуем взять диапазон символов из pos или range
                        if (len <= 1) {
                            if (err.pos && err.pos.length >= 2) {
                                var diff = err.pos[1] - err.pos[0];
                                if (diff > 0) len = diff;
                            } else if (err.range && err.range.length >= 2) {
                                var diffRange = err.range[1] - err.range[0];
                                if (diffRange > 0) len = diffRange;
                            }
                        }

                        errors.push({
                            message: err.message,
                            line: line,
                            column: col,
                            length: len
                        });
                    }
                } catch (e) {
                    errors.push({ message: e.message || String(e), line: 1, column: 1, length: 1 });
                }

                return errors;
            }
        ");
    }

    public IReadOnlyList<EditorError> ValidateSyntax(string yamlContent)
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
                Length = err.length != null ? (int)err.length : 1
            });
        }

        return result;
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}