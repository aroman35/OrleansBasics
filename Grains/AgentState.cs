using MongoDB.Driver;

namespace Grains
{
    [MongoCollection(CollectionNames.AgentsCollections)]
    public class AgentState : AggregateRoot
    {
        public abstract record AgentEventBase : EventBase<AgentState>;
    
        public record InitConnectionEvent(int TotalCores, int FreeCores, string[] Binaries) : AgentEventBase
        {
            public override UpdateDefinition<AgentState> UpdateDefinition =>
                Builders<AgentState>.Update.Combine(
                    Builders<AgentState>.Update.Set(x => x.IsEnabled, true),
                    Builders<AgentState>.Update.Set(x => x.TotalCores, TotalCores),
                    Builders<AgentState>.Update.Set(x => x.FreeCores, FreeCores),
                    Builders<AgentState>.Update.Set(x => x.Binaries, Binaries)
                );
        }

        public record DisconnectEvent : AgentEventBase
        {
            public override UpdateDefinition<AgentState> UpdateDefinition =>
                Builders<AgentState>.Update.Set(x => x.IsEnabled, false);
        }

        public record UpdateCoresInfoEvent(int TotalCores, int FreeCores) : AgentEventBase
        {
            public override UpdateDefinition<AgentState> UpdateDefinition =>
                Builders<AgentState>.Update.Combine(
                    Builders<AgentState>.Update.Set(x => x.FreeCores, FreeCores),
                    Builders<AgentState>.Update.Set(x => x.TotalCores, TotalCores));
        }
        
        public int TotalCores { get; set; }
        public int FreeCores { get; set; }
        public bool IsEnabled { get; set; }
        public string[] Binaries { get; set; }

        public void Apply(InitConnectionEvent initConnectionEvent)
        {
            IsEnabled = true;
            TotalCores = initConnectionEvent.TotalCores;
            FreeCores = initConnectionEvent.FreeCores;
            Binaries = initConnectionEvent.Binaries;
        }

        public void Apply(UpdateCoresInfoEvent updateCoresInfoEvent)
        {
            FreeCores = updateCoresInfoEvent.FreeCores;
            TotalCores = updateCoresInfoEvent.TotalCores;
        }

        public void Apply(DisconnectEvent disconnectEvent)
        {
            IsEnabled = false;
        }

        public override string ToString()
        {
            return $"Agent[{Id}]: Cores: {FreeCores}/{TotalCores}. Binaries: {string.Join(';', Binaries)}";
        }
    }
}