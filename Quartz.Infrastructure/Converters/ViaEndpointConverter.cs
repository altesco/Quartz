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
        var scalar = parser.Consume<Scalar>();

        return new ViaEndpointDto
        {
            Name = scalar.Value
        };
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        if (value is ViaEndpointDto via)
        {
            emitter.Emit(new Scalar(null, "!via", via.Name ?? "", ScalarStyle.Plain, false, false));
        }
    }
}