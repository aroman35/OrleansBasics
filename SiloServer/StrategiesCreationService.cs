using System;
using System.Threading;
using System.Threading.Tasks;
using GrainInterfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;

namespace SiloServer
{
    public class StrategiesCreationService : BackgroundService
    {
        private readonly IClusterClient _client;
        private readonly ILogger _logger;

        public StrategiesCreationService(IClusterClient client, ILogger<StrategiesCreationService> logger)
        {
            _client = client;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            string[] binaries = {
                "test-0001",
                "test-0002",
                "test-0003",
                "test-0004",
                "test-0005",
            };
            for (var i = 0; i < 500000; i++)
            {
                try
                {
                    var randIndex = new Random().Next(0, 4);
                    await _client.GetGrain<IStrategy>(Guid.NewGuid()).Create($"strategy-{i:0000}", binaries[randIndex]);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Error");
                }
                finally
                {
                    if (i % 10 == 0)
                        await Task.Delay(1000, stoppingToken);
                }
            }
        }
    }
}