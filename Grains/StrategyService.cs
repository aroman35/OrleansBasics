using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GrainInterfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Concurrency;
using Orleans.Core;
using Orleans.Runtime;
using Orleans.Runtime.Services;

namespace Grains
{
    [Reentrant]
    public class StrategyService : GrainService, IStrategyService
    {
        private readonly ConcurrentQueue<IStrategy> _strategies;
        private readonly IAgentsServiceClient _agentsServiceClient;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly IGrainFactory _grainFactory;
        private readonly ILogger _logger;
        private int _counter;

        public StrategyService(IGrainFactory grainFactory, IGrainIdentity id, Silo silo, ILoggerFactory loggerFactory,
            IAgentsServiceClient agentsServiceClient, IHostApplicationLifetime hostApplicationLifetime)
            : base(id, silo, loggerFactory)
        {
            _strategies = new ConcurrentQueue<IStrategy>();
            _grainFactory = grainFactory;
            _agentsServiceClient = agentsServiceClient;
            _hostApplicationLifetime = hostApplicationLifetime;
            _logger = loggerFactory.CreateLogger<StrategyService>();
        }

        public Task AddToQueue(Guid id)
        {
            _strategies.Enqueue(_grainFactory.GetGrain<IStrategy>(id));
            _logger.LogInformation("Strategy '{Strategy}' added", id);
            Interlocked.Increment(ref _counter);
            return Task.CompletedTask;
        }

        public ValueTask<int> TotalCreatedStrategiesCount() => ValueTask.FromResult(_counter);

        public ValueTask<Guid> TryDeque()
        {
            return ValueTask.FromResult(_strategies.TryDequeue(out var strategy) ? strategy.GetPrimaryKey() : Guid.Empty);
        }
    }

    [Reentrant]
    public class AgentsService : GrainService, IAgentsService
    {
        private readonly ConcurrentDictionary<Guid, IAgentGrain> _agents;
        private readonly IStrategyServiceClient _strategyServiceClient;
        private readonly ILogger _logger;
        private readonly IGrainFactory _grainFactory;

        public AgentsService(
            IGrainFactory grainFactory,
            IGrainIdentity id,
            Silo silo,
            ILoggerFactory loggerFactory,
            IStrategyServiceClient strategyServiceClient)
            : base(id, silo, loggerFactory)
        {
            _grainFactory = grainFactory;
            _strategyServiceClient = strategyServiceClient;
            _agents = new ConcurrentDictionary<Guid, IAgentGrain>();
            _logger = loggerFactory.CreateLogger<AgentsService>();
        }

        public Task Add(Guid id)
        {
            var agentGrain = _grainFactory.GetGrain<IAgentGrain>(id);
            _agents.AddOrUpdate(id, _ => agentGrain, (_, _) => agentGrain);
            _logger.LogInformation("Added new agent");
            return Task.CompletedTask;
        }

        public async ValueTask<bool> HasFreeSlots()
        {
            if (_agents.IsEmpty) return await ValueTask.FromResult(false);
            return await _agents.Values.ToAsyncEnumerable().AllAwaitAsync(x => x.HasFreeCore());
        }
    }

    public class StrategyServiceClient : GrainServiceClient<IStrategyService>, IStrategyServiceClient
    {
        public StrategyServiceClient(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        public Task AddToQueue(Guid id) => GrainService.AddToQueue(id);
        public ValueTask<int> TotalCreatedStrategiesCount() => GrainService.TotalCreatedStrategiesCount();
        public ValueTask<Guid> TryDeque() => GrainService.TryDeque();
    }

    public class AgentsServiceClient : GrainServiceClient<IAgentsService>, IAgentsServiceClient
    {
        public AgentsServiceClient(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        public Task Add(Guid id) => GrainService.Add(id);

        public ValueTask<bool> HasFreeSlots() => GrainService.HasFreeSlots();
    }
}