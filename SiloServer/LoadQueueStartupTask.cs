using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GrainInterfaces;
using Grains;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Orleans;
using Orleans.Runtime;

namespace SiloServer
{
    public class LoadQueueStartupTask : IStartupTask
    {
        private readonly IMongoCollection<StrategyState> _mongoCollection;
        private readonly IStrategyServiceClient _strategyServiceClient;
        private readonly IGrainFactory _grainFactory;
        private readonly ILogger _logger;

        public LoadQueueStartupTask(IMongoDatabase database, ILogger<LoadQueueStartupTask> logger, IStrategyServiceClient strategyServiceClient, IGrainFactory grainFactory)
        {
            _logger = logger;
            _strategyServiceClient = strategyServiceClient;
            _grainFactory = grainFactory;
            _mongoCollection = database.GetCollection<StrategyState>(typeof(StrategyState).GetCustomAttribute<MongoCollectionAttribute>()?.CollectionName);
        }

        public async Task Execute(CancellationToken cancellationToken)
        {
            try
            {
                var strategies = await _mongoCollection
                    .Aggregate()
                    .Match(Builders<StrategyState>.Filter.Ne(x => x.Progress, 100))
                    .Project(x => new { x.Id })
                    .ToListAsync(cancellationToken);
                
                await Task.WhenAll(strategies.Select(x => _strategyServiceClient.AddToQueue(x.Id)));
                _logger.LogInformation("Loaded {Count} strategies from DB", strategies.Count);
                if (strategies.Count < 1000)
                    await CreateStrategiesMocks(1000 - strategies.Count, cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to load agents");
                throw;
            }
        }

        private async Task CreateStrategiesMocks(int count, CancellationToken cancellationToken)
        {
            string[] binaries = {
                "test-0001",
                "test-0002",
                "test-0003",
                "test-0004",
                "test-0005",
            };
            for (var i = 0; i < count; i++)
            {
                var randIndex = new Random().Next(0, 4);
                var id = Guid.NewGuid();
                await _grainFactory.GetGrain<IStrategy>(id).Create($"strategy-{i:0000}", binaries[randIndex]);
                await _strategyServiceClient.AddToQueue(id);
                if (i % 10000 == 0) await Task.Delay(1000, cancellationToken);
            }
        }
    }
}