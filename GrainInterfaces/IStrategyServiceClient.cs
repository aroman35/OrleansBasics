using Orleans.Services;

namespace GrainInterfaces
{
    public interface IStrategyServiceClient : IGrainServiceClient<IStrategyService>, IStrategyService
    {
    }

    public interface IAgentsServiceClient : IGrainServiceClient<IAgentsService>, IAgentsService
    {
    }
}