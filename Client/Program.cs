using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using GrainInterfaces;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Providers.MongoDB.Configuration;
using Orleans.Streams;
using Polly;

namespace Client
{
    public class Program
    {
        private static IClusterClient _client;
        private static IAgentGrain _agentGrain;
        private static IAgentObserver _agentObserver;
        private static AgentObserver _observer;
        private static int _totalCores;
        private static int _freeCores;

        private static string[] _binaries =
        {
            "test-0001",
            "test-0002",
            "test-0003",
            "test-0004",
            "test-0005",
        };

        public static async Task Main(string[] args)
        {
            var streamId = Guid.Parse("6F26EFDC-4997-46FD-B60C-493ED2D7820C");
            var policy = Policy.Handle<Exception>().WaitAndRetryForeverAsync(tryCount =>
            {
                Console.WriteLine($"Retrying[{tryCount}]...");
                return TimeSpan.FromSeconds(1);
            });
            
            var id = Environment.GetEnvironmentVariable("AGENT_ID") ?? Guid.NewGuid().ToString();
            var _= int.TryParse(Environment.GetEnvironmentVariable("TOTAL_CORES") ?? "8", out _totalCores);
            _freeCores = _totalCores;

            var testObserver = new TestObserver();
            await ConnectClient();
            await _client.Connect(ReconnectionHandler);
            _agentGrain = _client.GetGrain<IAgentGrain>(Guid.Parse(id));
            await _agentGrain.InitConnection(8, 8, _binaries);
            var streamProvider = _client.GetStreamProvider("strategies-queue");
            var stream = streamProvider.GetStream<int>(streamId, "TEST");
            await stream.SubscribeAsync(testObserver);
            
            // _observer = new AgentObserver(_agentGrain, _client, _totalCores);
            // _agentObserver = await _client.CreateObjectReference<IAgentObserver>(_observer);
            //
            // await policy.ExecuteAsync(SubscribeToQueue);

            while (true)
            {
                await Task.Delay(10000);
            }
        }

        private static async Task SubscribeToQueue()
        {
            while (true)
            {
                await _agentGrain.Subscribe(_agentObserver);
                await _agentGrain.InitConnection(_observer.TotalCores, _observer.FreeCores, _binaries);
                await Task.Delay(TimeSpan.FromMinutes(1));
                await _agentGrain.Unsubscribe(_agentObserver);
            }
        }
        
        private static async Task ConnectClient()
        {
            var serverIp = new IPEndPoint(IPAddress.Parse("192.168.50.61"), 30000);
            _client = new ClientBuilder()
                .UseMongoDBClient("mongodb://192.168.50.58:27017")
                .AddSimpleMessageStreamProvider("strategies-queue")
                .UseMongoDBClustering(options =>
                {
                    options.Strategy = MongoDBMembershipStrategy.SingleDocument;
                    options.DatabaseName = "orleans-cluster";
                })
                .Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = "test-cluster-1";
                    options.ServiceId = "test-service-1";
                })
                .Configure<SerializationProviderOptions>(options =>
                {
                    options.SerializationProviders = new List<Type> { typeof(Orleans.Serialization.ProtobufSerializer) };
                    options.FallbackSerializationProvider = typeof(Orleans.Serialization.BinaryFormatterSerializer);
                })
                .ConfigureLogging(logging => logging.AddConsole())
                .AddClusterConnectionLostHandler(ClusterConnectionLostHandler)
                .Build();
        }

        private static void ClusterConnectionLostHandler(object sender, EventArgs args)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("CONNECTION LOST!!!");
        }
        
        private static async Task<bool> ReconnectionHandler(Exception exception)
        {
            Console.WriteLine("Reconnection................................");
            Console.WriteLine(exception.Message);
            await Task.Delay(1000);
            return true;
        }
    }

    public class TestObserver : IAsyncObserver<int>
    {
        public Task OnNextAsync(int item, StreamSequenceToken token = null)
        {
            Console.WriteLine(item.ToString("000000000"));
            return Task.CompletedTask;
        }

        public Task OnCompletedAsync()
        {
            return Task.CompletedTask;
        }

        public Task OnErrorAsync(Exception ex)
        {
            return Task.CompletedTask;
        }
    }
}