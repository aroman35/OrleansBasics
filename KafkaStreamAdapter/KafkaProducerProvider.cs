using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace KafkaStreamAdapter
{
    public class KafkaProducerProvider
    {
        private readonly KafkaProducerFactory _kafkaProducerFactory;
        private readonly ConcurrentDictionary<Type, KafkaProducer> _producers;

        public KafkaProducerProvider(KafkaProducerFactory kafkaProducerFactory)
        {
            _kafkaProducerFactory = kafkaProducerFactory;
            _producers = new ConcurrentDictionary<Type, KafkaProducer>();
        }

        private KafkaProducer<TPayload> Take<TPayload>()
        {
            if (_producers.TryGetValue(typeof(TPayload), out var existingProducer))
                return existingProducer as KafkaProducer<TPayload>;

            var createdProducer = _kafkaProducerFactory.Produce<TPayload>();
            if (!_producers.TryAdd(typeof(TPayload), createdProducer))
                throw new Exception("Cannot create producer");

            return createdProducer as KafkaProducer<TPayload>;
        }

        public void SendBatch<TPayload>(IEnumerable<TPayload> items, string topic)
        {
            var producer = Take<TPayload>();
            foreach (var item in items)
                producer.Produce(item, topic);
            producer.Flush(5000);
        }
    }
}