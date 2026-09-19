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

        var jsTagsArray = YamlTagRegistry.GetTagsAsJsArray();

        _engine.Execute($@"
            const supportedTags = new Set({jsTagsArray});
            const emptyObjectTags = new Set({jsTagsArray});
        ");

        _engine.Execute(@"
            function positionFromOffset(text, offset) {
                if (typeof offset !== 'number' || offset < 0) {
                    return {
                        line: 1,
                        column: 1
                    };
                }

                let line = 1;
                let column = 1;

                for (let i = 0; i < offset && i < text.length; i++) {
                    if (text[i] === '\n') {
                        line++;
                        column = 1;
                    } else {
                        column++;
                    }
                }

                return {
                    line: line,
                    column: column
                };
            }

            function nodePosition(text, node) {
                if (!node || !node.range || node.range.length === 0) {
                    return {
                        line: 1,
                        column: 1,
                        length: 1
                    };
                }

                const startOffset = node.range[0];

                const endOffset =
                    typeof node.range[1] === 'number'
                        ? node.range[1]
                        : startOffset + 1;

                const pos =
                    positionFromOffset(
                        text,
                        startOffset);

                return {
                    line: pos.line,
                    column: pos.column,
                    length: Math.max(
                        1,
                        endOffset - startOffset)
                };
            }

            function addError(
                errors,
                message,
                text,
                node
            ) {
                const pos =
                    nodePosition(
                        text,
                        node);

                errors.push({
                    message: message,
                    line: pos.line,
                    column: pos.column,
                    length: pos.length
                });
            }

            function isEmptyTaggedObject(
                node,
                yamlLib
            ) {
                if (!node || !node.tag) {
                    return false;
                }

                if (!emptyObjectTags.has(node.tag)) {
                    return false;
                }

                if (yamlLib.isMap(node)) {
                    return true;
                }

                if (yamlLib.isScalar(node)) {
                    const value = node.value;

                    return (
                        value === null ||
                        value === undefined ||
                        value === ''
                    );
                }

                return false;
            }

            function validateObjectNode(
                errors,
                text,
                node,
                message,
                yamlLib
            ) {
                if (!node) {
                    addError(
                        errors,
                        message,
                        text,
                        node);

                    return false;
                }

                if (yamlLib.isMap(node) ||
                    isEmptyTaggedObject(node, yamlLib)) {
                    return true;
                }

                addError(
                    errors,
                    message,
                    text,
                    node);

                return false;
            }

            function getMapValue(
                map,
                keyName
            ) {
                if (!map) {
                    return null;
                }

                const items =
                    map.items || [];

                for (
                    let i = 0;
                    i < items.length;
                    i++
                ) {
                    const pair =
                        items[i];

                    if (!pair || !pair.key) {
                        continue;
                    }

                    if (pair.key.value === keyName) {
                        return pair.value || null;
                    }
                }

                return null;
            }

            function validateShape(
                errors,
                text,
                node,
                yamlLib
            ) {
                if (!node) {
                    return;
                }

                if (yamlLib.isMap(node) ||
                    isEmptyTaggedObject(node, yamlLib)) {
                    return;
                }

                addError(
                    errors,
                    'Свойство shape должно быть объектом',
                    text,
                    node);
            }

            function validatePin(
                errors,
                text,
                node,
                yamlLib
            ) {
                if (!validateObjectNode(
                        errors,
                        text,
                        node,
                        'Элемент pins должен быть объектом',
                        yamlLib)) {
                    return;
                }

                if (!yamlLib.isMap(node)) {
                    return;
                }

                const shape =
                    getMapValue(
                        node,
                        'shape');

                validateShape(
                    errors,
                    text,
                    shape,
                    yamlLib);

                if (shape &&
                    shape.tag === '!path') {
                    validatePath(
                        errors,
                        text,
                        shape,
                        yamlLib);
                }

                const point =
                    getMapValue(
                        node,
                        'point');

                if (point &&
                    !yamlLib.isMap(point)) {
                    addError(
                        errors,
                        'Свойство point должно быть объектом',
                        text,
                        point);
                }
            }

            function validateComponent(
                errors,
                text,
                node,
                yamlLib
            ) {
                if (!validateObjectNode(
                        errors,
                        text,
                        node,
                        'Элемент components должен быть объектом',
                        yamlLib)) {
                    return;
                }

                if (!yamlLib.isMap(node)) {
                    return;
                }

                const shape =
                    getMapValue(
                        node,
                        'shape');

                validateShape(
                    errors,
                    text,
                    shape,
                    yamlLib);

                if (shape &&
                    shape.tag === '!path') {
                    validatePath(
                        errors,
                        text,
                        shape,
                        yamlLib);
                }

                const pins =
                    getMapValue(
                        node,
                        'pins');

                if (!pins) {
                    return;
                }

                if (!yamlLib.isSeq(pins)) {
                    addError(
                        errors,
                        'Свойство pins должно быть списком',
                        text,
                        pins);

                    return;
                }

                const items =
                    pins.items || [];

                for (
                    let i = 0;
                    i < items.length;
                    i++
                ) {
                    validatePin(
                        errors,
                        text,
                        items[i],
                        yamlLib);
                }
            }

            function validatePath(
                errors,
                text,
                node,
                yamlLib
            ) {
                if (!node) {
                    return;
                }

                if (isEmptyTaggedObject(
                        node,
                        yamlLib)) {
                    return;
                }

                if (!yamlLib.isMap(node)) {
                    addError(
                        errors,
                        'Свойство shape должно быть объектом',
                        text,
                        node);

                    return;
                }

                const segments =
                    getMapValue(
                        node,
                        'segments');

                if (!segments) {
                    return;
                }

                if (!yamlLib.isSeq(segments)) {
                    addError(
                        errors,
                        'Свойство segments должно быть списком',
                        text,
                        segments);

                    return;
                }

                const items =
                    segments.items || [];

                for (
                    let i = 0;
                    i < items.length;
                    i++
                ) {
                    const segment =
                        items[i];

                    if (yamlLib.isMap(segment) ||
                        isEmptyTaggedObject(
                            segment,
                            yamlLib)) {
                        continue;
                    }

                    addError(
                        errors,
                        'Элемент segments должен быть объектом',
                        text,
                        segment);
                }
            }

            function validateTrace(
                errors,
                text,
                node,
                yamlLib
            ) {
                if (isEmptyTaggedObject(
                        node,
                        yamlLib)) {
                    return;
                }

                if (!yamlLib.isMap(node)) {
                    addError(
                        errors,
                        'Элемент traces должен быть объектом',
                        text,
                        node);

                    return;
                }

                const from =
                    getMapValue(
                        node,
                        'from');

                if (from &&
                    !yamlLib.isMap(from)) {
                    addError(
                        errors,
                        'Свойство from должно быть объектом',
                        text,
                        from);
                }

                const to =
                    getMapValue(
                        node,
                        'to');

                if (to &&
                    !yamlLib.isMap(to)) {
                    addError(
                        errors,
                        'Свойство to должно быть объектом',
                        text,
                        to);
                }
            }

            function validateStructure(
                text,
                doc,
                errors,
                yamlLib
            ) {
                const root =
                    doc.contents;

                if (!root) {
                    return;
                }

                if (!yamlLib.isMap(root)) {
                    addError(
                        errors,
                        'Корневой элемент YAML должен быть объектом',
                        text,
                        root);

                    return;
                }

                const components =
                    getMapValue(
                        root,
                        'components');

                if (components) {
                    if (!yamlLib.isSeq(components)) {
                        addError(
                            errors,
                            'Свойство components должно быть списком',
                            text,
                            components);
                    }
                    else {
                        const items =
                            components.items || [];

                        for (
                            let i = 0;
                            i < items.length;
                            i++
                        ) {
                            validateComponent(
                                errors,
                                text,
                                items[i],
                                yamlLib);
                        }
                    }
                }

                const layerShape =
                    getMapValue(
                        root,
                        'shape');

                if (layerShape) {
                    validateShape(
                        errors,
                        text,
                        layerShape,
                        yamlLib);

                    if (layerShape.tag === '!path') {
                        validatePath(
                            errors,
                            text,
                            layerShape,
                            yamlLib);
                    }
                }

                const traces =
                    getMapValue(
                        root,
                        'traces');

                if (traces) {
                    if (!yamlLib.isSeq(traces)) {
                        addError(
                            errors,
                            'Свойство traces должно быть списком',
                            text,
                            traces);
                    }
                    else {
                        const items =
                            traces.items || [];

                        for (
                            let i = 0;
                            i < items.length;
                            i++
                        ) {
                            validateTrace(
                                errors,
                                text,
                                items[i],
                                yamlLib);
                        }
                    }
                }
            }

            function validateYaml(text) {
                const errors = [];

                if (!text) {
                    return errors;
                }

                try {
                    const yamlLib =
                        globalThis.YAML ||
                        (
                            typeof YAML !== 'undefined'
                                ? YAML
                                : null
                        );

                    if (!yamlLib ||
                        typeof yamlLib.parseDocument !== 'function') {
                        return [{
                            message:
                                'Ошибка инициализации: объект YAML не найден в V8 context',

                            line: 1,
                            column: 1,
                            length: 1
                        }];
                    }

                    const doc =
                        yamlLib.parseDocument(
                            text,
                            {
                                prettyErrors: true
                            });

                    const syntaxErrors =
                        doc.errors || [];

                    for (
                        let i = 0;
                        i < syntaxErrors.length;
                        i++
                    ) {
                        const err =
                            syntaxErrors[i];

                        let line = 1;
                        let column = 1;
                        let length = 1;

                        if (err.linePos &&
                            err.linePos.length > 0) {
                            line =
                                err.linePos[0].line;

                            column =
                                err.linePos[0].col;

                            if (
                                err.linePos.length > 1 &&
                                err.linePos[1].line === line
                            ) {
                                length =
                                    Math.max(
                                        1,
                                        err.linePos[1].col -
                                        column);
                            }
                        }

                        if (err.pos &&
                            err.pos.length >= 2) {
                            const diff =
                                err.pos[1] -
                                err.pos[0];

                            if (diff > 0) {
                                length = diff;
                            }
                        }
                        else if (
                            err.range &&
                            err.range.length >= 2
                        ) {
                            const diffRange =
                                err.range[1] -
                                err.range[0];

                            if (diffRange > 0) {
                                length = diffRange;
                            }
                        }

                        errors.push({
                            message:
                                err.message,

                            line:
                                line,

                            column:
                                column,

                            length:
                                Math.max(
                                    1,
                                    length)
                        });
                    }

                    if (errors.length === 0) {
                        validateStructure(
                            text,
                            doc,
                            errors,
                            yamlLib);
                    }
                }
                catch (e) {
                    errors.push({
                        message:
                            e.message ||
                            String(e),

                        line: 1,
                        column: 1,
                        length: 1
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
                Length = err.length != null
                    ? Math.Max(1, (int)err.length)
                    : 1
            });
        }

        return result;
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}