namespace ResumeParserBackend.Helper;

using Confluent.Kafka;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed class KafkaSingleton
{
    private static readonly Lazy<KafkaSingleton> _instance = new(() => new KafkaSingleton());

    private KafkaSingleton()
    {
        var bootstrapServers = $"{ConfigManager.Instance.Get(c => c.Kafka.Host)}:{ConfigManager.Instance.Get(c => c.Kafka.Port)}";
        // 配置生产者
        ProducerConfig = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All // 确保可靠性
        };

        // 配置消费者
        ConsumerConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = ConfigManager.Instance.Get(c => c.Kafka.Group),
            AutoOffsetReset = AutoOffsetReset.Earliest, // 从最早的偏移量开始消费
            EnableAutoCommit = true // 自动提交偏移量
        };
    }
        
    public static KafkaSingleton Instance => _instance.Value;

    public ProducerConfig ProducerConfig { get; }

    public ConsumerConfig ConsumerConfig { get; }
}

public class KafkaHelper<TKey, TValue>
{
    private readonly ProducerConfig _producerConfig = KafkaSingleton.Instance.ProducerConfig;
    private readonly ConsumerConfig _consumerConfig = KafkaSingleton.Instance.ConsumerConfig;

    // 生产消息
    public async Task ProduceAsync(string topic, TKey key, TValue value)
    {
        using var producer = new ProducerBuilder<TKey, TValue>(_producerConfig)
            .SetKeySerializer(new Confluent.Kafka.Serializers.StringSerializer())
            .SetValueSerializer(new Confluent.Kafka.Serializers.StringSerializer())
            .Build();

        try
        {
            var result = await producer.ProduceAsync(topic, new Message<TKey, TValue>
            {
                Key = key,
                Value = value
            });

            Console.WriteLine($"Message sent to {result.TopicPartitionOffset}");
        }
        catch (ProduceException<TKey, TValue> e)
        {
            Console.WriteLine($"Failed to deliver message: {e.Error.Reason}");
            throw;
        }
    }

    // 消费消息（带回调）
    public void Consume(string topic, Action<ConsumeResult<TKey, TValue>> messageHandler, CancellationToken cancellationToken)
    {
        using var consumer = new ConsumerBuilder<TKey, TValue>(_consumerConfig)
            .SetKeyDeserializer(new Confluent.Kafka.Serializers.StringDeserializer())
            .SetValueDeserializer(new Confluent.Kafka.Serializers.StringDeserializer())
            .Build();

        consumer.Subscribe(topic);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(cancellationToken);
                    messageHandler(consumeResult); // 处理消息
                }
                catch (ConsumeException e)
                {
                    Console.WriteLine($"Error occured: {e.Error.Reason}");
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }

    // 批量生产消息
    public async Task ProduceManyAsync(string topic, IEnumerable<KeyValuePair<TKey, TValue>> messages)
    {
        using var producer = new ProducerBuilder<TKey, TValue>(_producerConfig)
            .SetKeySerializer(new Confluent.Kafka.Serializers.StringSerializer())
            .SetValueSerializer(new Confluent.Kafka.Serializers.StringSerializer())
            .Build();

        var tasks = new List<Task>();
        foreach (var message in messages)
        {
            tasks.Add(producer.ProduceAsync(topic, new Message<TKey, TValue>
            {
                Key = message.Key,
                Value = message.Value
            }));
        }

        await Task.WhenAll(tasks);
    }
}