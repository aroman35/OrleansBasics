using System;
using System.Threading.Tasks;
using GrainInterfaces;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Orleans;
using Orleans.Providers;
using Orleans.Runtime;

namespace Grains
{
    [MongoCollection(CollectionNames.StrategiesCollection)]
    public class StrategyState : AggregateRoot
    {
        public abstract record StrategyEventBase : EventBase<StrategyState>;
        public record UpdateProgressEvent(double Progress) : StrategyEventBase
        {
            public override UpdateDefinition<StrategyState> UpdateDefinition =>
                Builders<StrategyState>.Update.Set(x => x.Progress, Progress);
        }

        public record SetResultEvent(string Result) : StrategyEventBase
        {
            public override UpdateDefinition<StrategyState> UpdateDefinition =>
                Builders<StrategyState>.Update.Set(x => x.Result, Result);
        }

        public record CreateEvent(string Name, string Binary, DateTime Created) : StrategyEventBase
        {
            public override UpdateDefinition<StrategyState> UpdateDefinition =>
                Builders<StrategyState>.Update.Combine(
                    Builders<StrategyState>.Update.Set(x => x.Name, Name),
                    Builders<StrategyState>.Update.Set(x => x.Created, Created),
                    Builders<StrategyState>.Update.Set(x => x.Binary, Binary));
        }

        public record SetAgentEvent(Guid AgentId) : StrategyEventBase
        {
            public override UpdateDefinition<StrategyState> UpdateDefinition => Builders<StrategyState>.Update.Set(x => x.AgentId, AgentId);
        }

        public void Apply(UpdateProgressEvent @event)
        {
            Progress = @event.Progress;
        }
        public void Apply(SetResultEvent @event)
        {
            Result = @event.Result;
        }

        public void Apply(CreateEvent @event)
        {
            Name = @event.Name;
            Created = @event.Created;
        }

        public void Apply(SetAgentEvent @event)
        {
            AgentId = @event.AgentId;
        }
        
        public double Progress { get; private set; }
        public string Result { get; private set; }
        public string Name { get; private set; }
        public string Binary { get; private set; }
        public DateTime Created { get; private set; }
        public Guid AgentId { get; private set; }

        public override string ToString()
        {
            return $"Strategy[{Id}]: Progress: {Progress}; Result: {Result}";
        }
    }

    
    [LogConsistencyProvider(ProviderName = "CustomStorage")]
    public class StrategyGrain : PersistentGrain<StrategyState, StrategyState.StrategyEventBase>, IStrategy, IRemindable
    {
        private readonly IStrategyServiceClient _strategyServiceClient;
        private IGrainReminder _jobMonitor;

        public StrategyGrain(
            IMongoDatabase database,
            ILogger<StrategyGrain> logger, IStrategyServiceClient strategyServiceClient
        )
            : base(database, logger)
        {
            _strategyServiceClient = strategyServiceClient;
        }

        public async Task<Guid> Create(string name, string binary)
        {
            RaiseEvent(new StrategyState.CreateEvent(name, binary, DateTime.UtcNow));
            Logger.LogInformation("Created new strategy '{Id}'", this.GetPrimaryKey());
            await Load();
            return this.GetPrimaryKey();
        }

        public async Task Load()
        {
            await _strategyServiceClient.AddToQueue(this.GetPrimaryKey());
        }

        public async Task Start(Guid agentId)
        {
            RaiseEvent(new StrategyState.SetAgentEvent(agentId));
            _jobMonitor = await RegisterOrUpdateReminder("job-monitor", TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public Task UpdateProgress(double progress)
        {
            RaiseEvent(new StrategyState.UpdateProgressEvent(progress));
            return Task.CompletedTask;
        }

        public async Task Finish(string result)
        {
            RaiseEvent(new StrategyState.SetResultEvent(result));
            await this.ConfirmEvents();
            var reminder = await this.GetReminder("job-monitor");
            if (reminder is not null)
                await UnregisterReminder(reminder);
            DeactivateOnIdle();
        }

        protected override void OnStateChanged()
        {
            Logger.LogInformation("State: {State}", State.ToString());
            base.OnStateChanged();
        }

        public override async Task OnDeactivateAsync()
        {
            Logger.LogInformation(
                "Strategy '{Id}' deactivated. Progress: {Progress}; Result: {Result}",
                this.GetPrimaryKey(),
                State.Progress,
                State.Result);

            await base.OnDeactivateAsync();
        }

        public async Task ReceiveReminder(string reminderName, TickStatus status)
        {
            if (string.IsNullOrEmpty(State.Result))
            {
                RaiseEvent(new StrategyState.UpdateProgressEvent(0));
                await _strategyServiceClient.AddToQueue(this.GetPrimaryKey());
                Logger.LogWarning("Strategy {StrategyId} was not responding", this.GetPrimaryKey());
            }
        }
    }
}