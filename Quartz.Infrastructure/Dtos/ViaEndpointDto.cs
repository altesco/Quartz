namespace Quartz.Infrastructure.Dtos;

public record ViaEndpointDto : EndpointBaseDto
{
    public string? Name { get; init; }
}