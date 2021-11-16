using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GrainInterfaces;
using Grains;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Newtonsoft.Json;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Providers.MongoDB.Configuration;
using Orleans.Statistics;
using Serilog;

namespace SiloServer
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            using var host = CreateHostBuilder(args).Build();
            await host.RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration))
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .ConfigureAppConfiguration(config => config
                            .AddEnvironmentVariables()
                            .AddJsonFile("loggerSettings.json", false))
                        .UseKestrel(config => config.ListenLocalhost(5300))
                        .ConfigureServices(services => services
                            .AddSingleton<IMongoClient>(new MongoClient(new MongoClientSettings
                            {
                                Server = new MongoServerAddress("192.168.50.58", 27017),
                                MaxConnectionPoolSize = 100000
                            }))
                            .AddSingleton(provider => provider.GetRequiredService<IMongoClient>().GetDatabase("orleans-backtests-db"))
                        );
                    webBuilder.UseStartup<Startup>();
                })
                .UseOrleans((context, builder) => builder
                    .AddGrainService<StrategyService>()
                    .AddGrainService<AgentsService>()
                    // .AddStartupTask<LoadAgentsStartupTask>()
                    // .AddStartupTask<LoadQueueStartupTask>()
                    .AddSimpleMessageStreamProvider("strategies-queue")
                    .AddMemoryGrainStorage("PubSubStore")
                    .ConfigureServices(services => services
                        .AddSingleton<IStrategyServiceClient, StrategyServiceClient>()
                        .AddSingleton<IAgentsServiceClient, AgentsServiceClient>()
                        // .AddHostedService<StrategiesCreationService>()
                    )
                    .UseMongoDBClustering(options =>
                    {
                        options.Strategy = MongoDBMembershipStrategy.SingleDocument;
                        options.DatabaseName = "orleans-cluster";
                    })
                    .ConfigureEndpoints(5200, 30000)
                    .UseMongoDBClient("mongodb://192.168.50.58:27017")
                    .UseMongoDBReminders(options => { options.DatabaseName = "orleans-reminders"; })
                    .AddMongoDBGrainStorageAsDefault(optionsBuilder => optionsBuilder.Configure(options =>
                    {
                        options.DatabaseName = "orleans";
                        options.ConfigureJsonSerializerSettings = settings =>
                        {
                            settings.NullValueHandling = NullValueHandling.Include;
                            settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
                            settings.DefaultValueHandling = DefaultValueHandling.Populate;
                        };
                    }))
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
                    .UseLinuxEnvironmentStatistics()
                    .ConfigureApplicationParts(parts => parts
                        .AddApplicationPart(typeof(StrategyGrain).Assembly)
                        .WithReferences()
                    )
                    .AddCustomStorageBasedLogConsistencyProvider("CustomStorage")
                );
    }
}