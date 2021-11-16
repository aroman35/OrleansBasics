using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Orleans;
using Orleans.EventSourcing;
using Orleans.EventSourcing.CustomStorage;
using Orleans.Runtime;

namespace Grains
{
    public abstract class PersistentGrain<TState, TEvent> :
        JournaledGrain<TState, TEvent>,
        ICustomStorageInterface<TState, TEvent>

        where TState : AggregateRoot, new()
        where TEvent : EventBase<TState>
    {
        private readonly IMongoCollection<TState> _mongoCollection;
        private protected readonly ILogger Logger;

        protected PersistentGrain(
            IMongoDatabase database,
            ILogger<PersistentGrain<TState, TEvent>> logger)
        {
            Logger = logger;
            _mongoCollection = database.GetCollection<TState>(typeof(TState).GetCustomAttribute<MongoCollectionAttribute>()?.CollectionName ??
                                                              typeof(TState).Name.ToLower() + "s");
        }

        public async Task<KeyValuePair<int, TState>> ReadStateFromStorage()
        {
            Logger.Debug("Reading state from db...");
            var find = Builders<TState>.Filter.Eq(x => x.Id, this.GetPrimaryKey());
            var document = await _mongoCollection.Find(find).FirstOrDefaultAsync() ?? new TState
            {
                Id = this.GetPrimaryKey()
            };
            return new KeyValuePair<int, TState>(1, document);
        }

        public async Task<bool> ApplyUpdatesToStorage(IReadOnlyList<TEvent> updates, int expectedversion)
        {
            Logger.Debug("Pushing changes to db...");
            var find = Builders<TState>.Filter.Eq(x => x.Id, this.GetPrimaryKey());
            var updateDefinition = Builders<TState>.Update.Combine(updates.Select(x => x.UpdateDefinition));
            var result = await _mongoCollection.UpdateOneAsync(find, updateDefinition, new UpdateOptions
            {
                IsUpsert = true
            });
            return result.IsAcknowledged;
        }
    }

    public static class CollectionNames
    {
        public const string StrategiesCollection = "strategies";
        public const string AgentsCollections = "agents";

        public static class Storages
        {
            public const string Storage = "-storage";
            
            public const string StrategiesStorage = StrategiesCollection + Storage;
            public const string AgentsStorage = AgentsCollections + Storage;
        }
    }
}