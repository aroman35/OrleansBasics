using System;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace KafkaStreamAdapter
{
    public class KafkaProducer<TPayload> : KafkaProducer
    {
        private readonly IProducer<Null, KafkaMessage<TPayload>> _producer;
        private readonly ILogger _logger;

        public KafkaProducer(ILogger logger)
        {
            _logger = logger;
            _logger = logger;
            var producerBuilder = new ProducerConfig
            {
                ClientId = "test",
                BootstrapServers = "localhost:9092,localhost:9093,localhost:9094",
                SecurityProtocol = SecurityProtocol.Plaintext,
                EnableDeliveryReports = true,
                MessageTimeoutMs = 0,
                BatchNumMessages = 1_000_000,
                QueueBufferingMaxMessages = 1_000_000,
                MessageSendMaxRetries = 3
            };
            
            _producer = new ProducerBuilder<Null, KafkaMessage<TPayload>>(producerBuilder)
                .SetValueSerializer(KafkaValueConverter<TPayload>.Instance())
                .SetErrorHandler((_, error) => _logger.LogError("Error producing message {Error}", error.Reason))
                .SetLogHandler((_, logMessage) => _logger.Log(
                    (LogLevel)logMessage.LevelAs(LogLevelType.MicrosoftExtensionsLogging),
                    "Kafka {Facility} producer {Producer}: {Message}", logMessage.Facility, logMessage.Name, logMessage.Message))
                .Build();
        }

        public override void Flush(int timeoutMs)
        {
            _producer.Flush(TimeSpan.FromMilliseconds(timeoutMs));
        }
        
        public void Produce(TPayload payload, string topic)
        {

            var message = new Message<Null, KafkaMessage<TPayload>>
            {
                Value = new KafkaMessage<TPayload>(payload)
            };
            
            _producer.Produce(topic, message, DeliveryHandler);
        }

        private void DeliveryHandler(DeliveryReport<Null, KafkaMessage<TPayload>> report)
        {
            if (report.Error.IsError)
                _logger.LogError("Error producing message {Error}", report.Error.Reason);
            
            _logger.LogDebug("Message delivered {Topic}[{Partition}]@{Offset}", report.Topic, report.Partition.Value, report.Offset.Value);
        }
    }

    public abstract class KafkaProducer
    {
        public abstract void Flush(int timeoutMs);
    }
}