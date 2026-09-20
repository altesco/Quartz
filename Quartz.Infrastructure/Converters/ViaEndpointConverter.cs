using Quartz.Infrastructure.Dtos;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Converters;

public class ViaEndpointConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(ViaEndpointDto);

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        // 1. Обычный вариант: !via VIA_1
        if (parser.TryConsume<Scalar>(out var scalar))
        {
            return new ViaEndpointDto
            {
                Name = scalar.Value
            };
        }

        // 2. Если пользователь написал !via в форме маппинга
        if (parser.Current is MappingStart)
        {
            string? name = null;
            parser.Consume<MappingStart>();

            while (!parser.Accept<MappingEnd>(out _))
            {
                if (parser.TryConsume<Scalar>(out var keyScalar))
                {
                    if (string.Equals(keyScalar.Value, "name", StringComparison.OrdinalIgnoreCase))
                    {
                        if (parser.TryConsume<Scalar>(out var valueScalar))
                        {
                            name = valueScalar.Value;
                        }
                        else
                        {
                            rootDeserializer(typeof(object));
                        }
                    }
                    else
                    {
                        // Пропускаем значение неизвестного ключа целиком
                        rootDeserializer(typeof(object));
                    }
                }
                else
                {
                    parser.MoveNext();
                }
            }

            parser.Consume<MappingEnd>();

            return new ViaEndpointDto
            {
                Name = name
            };
        }

        // 3. Если там недописанный ввод — просто сдвигаем парсер дальше через MoveNext()
        parser.MoveNext();
        return new ViaEndpointDto();
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        if (value is ViaEndpointDto via)
        {
            emitter.Emit(new Scalar(null, "!via", via.Name ?? "", ScalarStyle.Plain, false, false));
        }
    }
}