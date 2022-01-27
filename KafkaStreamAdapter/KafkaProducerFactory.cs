using Microsoft.Extensions.Logging;

namespace KafkaStreamAdapter
{
    public class KafkaProducerFactory
    {
        private readonly ILoggerFactory _loggerFactory;

        public KafkaProducerFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public KafkaProducer Produce<TPayload>()
        {
            return new KafkaProducer<TPayload>(_loggerFactory.CreateLogger<KafkaProducer<TPayload>>());
        }
    }
}