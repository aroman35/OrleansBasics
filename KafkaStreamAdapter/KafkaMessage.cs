namespace KafkaStreamAdapter
{
    public class KafkaMessage<TPayload>
    {
        public static KafkaMessage<TPayload> Empty()
        {
            return new KafkaMessage<TPayload>
            {
                Payload = default
            };
        }

        private KafkaMessage()
        {
        }
        
        public KafkaMessage(TPayload payload)
        {
            Payload = payload;
        }

        public TPayload Payload { get; private set; }
    }
}