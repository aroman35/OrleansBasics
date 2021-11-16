using System;
using System.Threading.Tasks;
using Orleans.Services;

namespace GrainInterfaces
{
    public interface IStrategyService : IGrainService
    {
        Task AddToQueue(Guid id);
        ValueTask<int> TotalCreatedStrategiesCount();
        ValueTask<Guid> TryDeque();
    }

    public interface IAgentsService : IGrainService
    {
        Task Add(Guid id);
        ValueTask<bool> HasFreeSlots();
    }
}