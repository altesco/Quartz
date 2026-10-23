using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public abstract record BoardEntityDto
{   
    [YamlIgnore]
    public long Line { get; set; }

    [YamlIgnore]
    public long Column { get; set; }

    [YamlIgnore]
    public long Length { get; set; }
}