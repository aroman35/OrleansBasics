using System;
using System.Text;
using Confluent.Kafka;
using Newtonsoft.Json;

namespace KafkaStreamAdapter
{
    public class KafkaValueConverter<TPayload> : ISerializer<KafkaMessage<TPayload>>, IDeserializer<KafkaMessage<TPayload>>
    {
        private static KafkaValueConverter<TPayload> _instance;
        // ReSharper disable once StaticMemberInGenericType
        private static readonly object Sync = new();
        public static KafkaValueConverter<TPayload> Instance()
        {
            lock(Sync)
                return _instance ??= new KafkaValueConverter<TPayload>();
        }
        
        public byte[] Serialize(KafkaMessage<TPayload> data, SerializationContext context)
        {
            var json = JsonConvert.SerializeObject(data);
            return Encoding.UTF8.GetBytes(json);
        }

        public KafkaMessage<TPayload> Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
        {
            if (isNull)
                return KafkaMessage<TPayload>.Empty();
            var json = Encoding.UTF8.GetString(data);
            
            return JsonConvert.DeserializeObject<KafkaMessage<TPayload>>(json);
        }
    }
    
    public class KafkaKeyConverter<TKey> : ISerializer<TKey>, IDeserializer<TKey>
    {
        private static KafkaKeyConverter<TKey> _instance;
        // ReSharper disable once StaticMemberInGenericType
        private static readonly object Sync = new();
        public static KafkaKeyConverter<TKey> Instance()
        {
            lock(Sync)
                return _instance ??= new KafkaKeyConverter<TKey>();
        }
        
        public byte[] Serialize(TKey data, SerializationContext context)
        {
            var json = JsonConvert.SerializeObject(data);
            return Encoding.UTF8.GetBytes(json);
        }

        public TKey Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
        {
            if (isNull)
                return default;
            var json = Encoding.UTF8.GetString(data);
            
            return JsonConvert.DeserializeObject<TKey>(json);
        }
    }
}