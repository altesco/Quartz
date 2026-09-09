using Quartz.Core.Models;

namespace Quartz.Application.Interfaces;

public interface IBoardProcessingCoordinator
{
    LayerProcessResult Process(string text);
}