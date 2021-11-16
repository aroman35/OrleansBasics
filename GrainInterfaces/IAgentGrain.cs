using System;
using System.Threading.Tasks;
using Orleans;

namespace GrainInterfaces
{
    public interface IAgentGrain : IGrainWithGuidKey
    {
        Task InitConnection(int coresCount, int freeCores, string[] binaries);
        Task UpdateCoresInfo(int totalCores, int freeCores);
        Task<Guid> TakeStrategy();
        ValueTask<bool> HasFreeCore();
        Task Subscribe(IAgentObserver observer);
        Task Unsubscribe(IAgentObserver observer);
    }

    public interface IAgentObserver : IGrainObserver
    {
        Task ReceiveStrategy(Guid strategyId);
    }
}