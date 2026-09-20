using System.ComponentModel.DataAnnotations;

namespace Quartz.Infrastructure.Dtos;

public record ViaEndpointDto : EndpointBaseDto
{
    [Required]
    public string? Name { get; init; }
}