using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using GrainInterfaces;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Providers;
using Orleans.Providers.Streams.Common;
using Orleans.Serialization;
using Orleans.Streams;
using Polly;

namespace Client
{
    public class AgentObserver : IAgentObserver
    {
        private readonly IAgentGrain _agentGrain;
        private readonly IClusterClient _client;
        private readonly Stopwatch _globalWath;
        private readonly SemaphoreSlim _semaphore;
        public int TotalCores => _totalCores;
        public int FreeCores => _freeCores;
        
        private int _totalHandled;
        private int _errorsCount;
        private long _avgDuration;
        private int _freeCores;
        private int _totalCores;

        public AgentObserver(IAgentGrain agentGrain, IClusterClient client, int totalCores)
        {
            _agentGrain = agentGrain;
            _client = client;
            _totalCores = totalCores;
            _freeCores = totalCores;
            _globalWath = Stopwatch.StartNew();
            _semaphore = new SemaphoreSlim(FreeCores, TotalCores);
        }

        public async Task ReceiveStrategy(Guid strategyId)
        {
            await _semaphore.WaitAsync();
            Interlocked.Decrement(ref _freeCores);
            await _agentGrain.UpdateCoresInfo(TotalCores, FreeCores);

            var policy = Policy.Handle<Exception>()
                .WaitAndRetryForeverAsync(
                    _ => TimeSpan.FromSeconds(1),
                    (exception, _) => Console.WriteLine($"Retrying... {exception.Message}"));
            
            await Task.Factory.StartNew(() => policy.ExecuteAndCaptureAsync(() => DoClientWork(strategyId)), TaskCreationOptions.LongRunning);
        }

        private async Task DoClientWork(Guid strategyId)
        {
            try
            {
                var stopWatch = Stopwatch.StartNew();

                if (FreeCores < 0)
                    throw new Exception("Cores finished!!!");
                if (FreeCores > TotalCores)
                    throw new Exception("Cores raised to stratosphere!!!");

                var backtest = _client.GetGrain<IStrategy>(strategyId);

                await backtest.Start(_agentGrain.GetPrimaryKey());
                await Task.Delay(1000);
                var progress = 0;
                var progressStep = 20;
                while (progress <= 100)
                {
                    await backtest.UpdateProgress(progress);
                    progress += progressStep;
                    await Task.Delay(1000);
                }

                await backtest.Finish("Very good PnL");
                Interlocked.Increment(ref _freeCores);
                await _agentGrain.UpdateCoresInfo(TotalCores, FreeCores);
                _semaphore.Release();
                stopWatch.Stop();
                Interlocked.Increment(ref _totalHandled);
                _avgDuration = (_avgDuration + stopWatch.ElapsedTicks) / 2;
            }
            catch (Exception exception)
            {
                Interlocked.Increment(ref _errorsCount);
                throw;
            }
            finally
            {
                if (_totalHandled == 103292)
                    _globalWath.Stop();
                
                Console.ForegroundColor = _errorsCount > 0 ? ConsoleColor.Red : ConsoleColor.Green;
                Console.WriteLine($"Avg duration: {TimeSpan.FromTicks(_avgDuration):g}. Errors: {_errorsCount}. Total: {_totalHandled}. Global time: {_globalWath.Elapsed:g}");
                Console.ResetColor();
            }
        }
    }
    
    public class KafkaAdapter : IQueueAdapter
    {
        public async Task QueueMessageBatchAsync<T>(Guid streamGuid, string streamNamespace, IEnumerable<T> events, StreamSequenceToken token, Dictionary<string, object> requestContext)
        {
            throw new NotImplementedException();
        }

        public IQueueAdapterReceiver CreateReceiver(QueueId queueId)
        {
            throw new NotImplementedException();
        }

        public string Name { get; }
        public bool IsRewindable { get; }
        public StreamProviderDirection Direction { get; }
    }
    
    public class KafkaReceiver : IQueueAdapterReceiver
    {
        public async Task Initialize(TimeSpan timeout)
        {
            throw new NotImplementedException();
        }

        public async Task<IList<IBatchContainer>> GetQueueMessagesAsync(int maxCount)
        {
            throw new NotImplementedException();
        }

        public async Task MessagesDeliveredAsync(IList<IBatchContainer> messages)
        {
            throw new NotImplementedException();
        }

        public async Task Shutdown(TimeSpan timeout)
        {
            throw new NotImplementedException();
        }
    }

    public class KafkaProvider : PersistentStreamProvider
    {
        public KafkaProvider(string name, StreamPubSubOptions pubsubOptions, StreamLifecycleOptions lifeCycleOptions, IProviderRuntime runtime, SerializationManager serializationManager, ILogger<PersistentStreamProvider> logger)
            : base(name, pubsubOptions, lifeCycleOptions, runtime, serializationManager, logger)
        {
        }
    }
}