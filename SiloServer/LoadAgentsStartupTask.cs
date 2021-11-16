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
    public class LoadAgentsStartupTask : IStartupTask
    {
        private readonly IMongoCollection<AgentState> _mongoCollection;
        private readonly IGrainFactory _grainFactory;
        private readonly ILogger _logger;

        public LoadAgentsStartupTask(IMongoDatabase database, ILogger<LoadAgentsStartupTask> logger, IGrainFactory grainFactory)
        {
            _logger = logger;
            _grainFactory = grainFactory;
            _mongoCollection = database.GetCollection<AgentState>(typeof(AgentState).GetCustomAttribute<MongoCollectionAttribute>()?.CollectionName);
        }
        
        public async Task Execute(CancellationToken cancellationToken)
        {
            try
            {
                var agents = await _mongoCollection.Find(x => true)
                    .Project(x => new { x.Id })
                    .ToListAsync(cancellationToken);
                
                var _ = agents.Select(x => _grainFactory.GetGrain<IAgentGrain>(x.Id)).ToArray();
                _logger.LogInformation("Loaded {Count} agents from DB", agents.Count);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to load agents");
                throw;
            }
        }
    }
}