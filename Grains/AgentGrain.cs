using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GrainInterfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Orleans;
using Orleans.Concurrency;
using Orleans.LogConsistency;
using Orleans.Providers;
using Orleans.Streams;

namespace Grains
{
    [LogConsistencyProvider(ProviderName = "CustomStorage")]
    public class AgentGrain : PersistentGrain<AgentState, AgentState.AgentEventBase>, IAgentGrain, IDisposable
    {
        private readonly IStrategyServiceClient _strategyServiceClient;
        private readonly IAgentsServiceClient _agentsServiceClient;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;

        private IDisposable _executionTimer;
        private IDisposable _streamTimer;
        private IAgentObserver _agentObserver;
        private IStrategiesQueueGrain _strategiesQueueGrain;
        private int _counter;

        public AgentGrain(
            IMongoDatabase database,
            ILogger<AgentGrain> logger,
            IStrategyServiceClient strategyServiceClient,
            IAgentsServiceClient agentsServiceClient,
            IHostApplicationLifetime applicationLifetime)
            : base(database, logger)
        {
            _strategyServiceClient = strategyServiceClient;
            _agentsServiceClient = agentsServiceClient;
            _hostApplicationLifetime = applicationLifetime;

            applicationLifetime.ApplicationStopping.Register(() => _executionTimer?.Dispose());
            applicationLifetime.ApplicationStopping.Register(() => _streamTimer?.Dispose());
        }

        private async Task GetJobs()
        {
            Logger.LogInformation("Get jobs triggered");
            while (!_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested && _agentObserver is not null && State.FreeCores > 0)
            {
                var strategyId = await _strategyServiceClient.TryDeque();
                if (strategyId == Guid.Empty) return;
                Logger.LogInformation("Found a strategy to be executed {StrategyId}", strategyId);
                await _agentObserver.ReceiveStrategy(strategyId);
            }
        }

        public async Task InitConnection(int coresCount, int freeCores, string[] binaries)
        {
            RaiseEvent(new AgentState.InitConnectionEvent(coresCount, freeCores, binaries));
            await _agentsServiceClient.Add(this.GetPrimaryKey());
            Logger.LogInformation("Initialized a new agent. Starting streaming...");
            _strategiesQueueGrain ??= GrainFactory.GetGrain<IStrategiesQueueGrain>(Guid.Parse("93AFE352-D894-4266-B425-6CD2C0626644"));
            await _strategiesQueueGrain.SendDataToStream(Enumerable.Range(_counter, 10));
            //_executionTimer = RegisterTimer(_ => GetJobs(), new object(), TimeSpan.Zero, TimeSpan.FromSeconds(5));
        }

        public async Task UpdateCoresInfo(int totalCores, int freeCores)
        {
            RaiseEvent(new AgentState.UpdateCoresInfoEvent(totalCores, freeCores));
            await ConfirmEvents();
        }

        public Task<Guid> TakeStrategy()
        {
            return _strategyServiceClient.TryDeque().AsTask();
        }

        public ValueTask<bool> HasFreeCore()
        {
            return ValueTask.FromResult(State.FreeCores > 0);
        }

        public Task Subscribe(IAgentObserver observer)
        {
            _agentObserver = observer;
            Logger.LogInformation("Agent subscribed on events");
            return Task.CompletedTask;
        }

        public Task Unsubscribe(IAgentObserver observer)
        {
            Logger.LogInformation("Agent unsubscribed from events");
            _agentObserver = null;
            return Task.CompletedTask;
        }

        protected override void OnStateChanged()
        {
            Logger.LogInformation("State update for Agent: {State}", State.ToString());
            base.OnStateChanged();
        }

        protected override void OnConnectionIssue(ConnectionIssue issue)
        {
            RaiseEvent(new AgentState.DisconnectEvent());
            base.OnConnectionIssue(issue);
        }

        protected override void OnConnectionIssueResolved(ConnectionIssue issue)
        {
            RaiseEvent(new AgentState.InitConnectionEvent(State.TotalCores, State.FreeCores, State.Binaries));
            base.OnConnectionIssueResolved(issue);
        }

        public override Task OnDeactivateAsync()
        {
            RaiseEvent(new AgentState.DisconnectEvent());
            _executionTimer?.Dispose();
            return base.OnDeactivateAsync();
        }

        public void Dispose()
        {
            _executionTimer?.Dispose();
        }
    }

    [StatelessWorker]
    public class StrategiesQueueGrain : Grain, IStrategiesQueueGrain
    {
        private IAsyncStream<int> _testStream;
        private readonly ILogger _logger;
        private int _lastValue;
        private IDisposable _job;

        public StrategiesQueueGrain(ILogger<StrategiesQueueGrain> logger)
        {
            _logger = logger;
        }

        public override async Task OnActivateAsync()
        {
            await base.OnActivateAsync();
            _logger.LogInformation("Agent grain created. Creating stream...");
            var streamProvider = GetStreamProvider("strategies-queue");
            _testStream = streamProvider.GetStream<int>(Guid.Parse("6F26EFDC-4997-46FD-B60C-493ED2D7820C"), "TEST");
        }

        public async Task SendDataToStream(int data)
        {
            _logger.LogInformation("Sending single item to stream");
            await _testStream.OnNextAsync(data);
        }

        public Task SendDataToStream(IEnumerable<int> data)
        {
            _job ??= RegisterTimer(_ => Send(), new object(), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            return Task.CompletedTask;
        }

        private async Task Send()
        {
            await _testStream.OnNextAsync(Interlocked.Increment(ref _lastValue));
        }
    }
}