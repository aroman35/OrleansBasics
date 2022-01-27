using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace KafkaStreamAdapter
{
    public class KafkaQueueAdapter : IQueueAdapter
    {
        private readonly ILogger _logger;
        private readonly KafkaProducerProvider _kafkaProducerProvider;

        public KafkaQueueAdapter(ILogger<KafkaQueueAdapter> logger, KafkaProducerProvider kafkaProducerProvider)
        {
            _kafkaProducerProvider = kafkaProducerProvider;
        }
        
        public async Task QueueMessageBatchAsync<TPayload>(Guid streamGuid, string streamNamespace, IEnumerable<TPayload> events, StreamSequenceToken token, Dictionary<string, object> requestContext)
        {
            _kafkaProducerProvider.SendBatch(events, streamNamespace);
        }

        public IQueueAdapterReceiver CreateReceiver(QueueId queueId)
        {
            throw new NotImplementedException();
        }

        public string Name { get; }
        public bool IsRewindable { get; }
        public StreamProviderDirection Direction { get; }
    }
}