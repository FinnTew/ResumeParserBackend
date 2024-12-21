namespace ResumeParserBackend.Helper;

using Confluent.Kafka;
using System.Text.Json;

public sealed class KafkaSingleton
{
    private static readonly Lazy<KafkaSingleton> _instance = new(() => new KafkaSingleton());

    private readonly ProducerConfig _producerConfig;
    private readonly ConsumerConfig _consumerConfig;

    private KafkaSingleton()
    {
        var bootstrapServers = $"{ConfigManager.Instance.Get(c => c.Kafka.Host)}:{ConfigManager.Instance.Get(c => c.Kafka.Port)}";
            
        // 配置生产者
        _producerConfig = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All // 确保可靠性
        };

        // 配置消费者
        _consumerConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = ConfigManager.Instance.Get(c => c.Kafka.Group),
            AutoOffsetReset = AutoOffsetReset.Earliest, // 从最早的偏移量开始消费
            EnableAutoCommit = true // 自动提交偏移量
        };
    }

    public static KafkaSingleton Instance => _instance.Value;

    public ProducerConfig ProducerConfig => _producerConfig;
    public ConsumerConfig ConsumerConfig => _consumerConfig;
}

public class KafkaHelper
{
    private readonly ProducerConfig _producerConfig = KafkaSingleton.Instance.ProducerConfig;
    private readonly ConsumerConfig _consumerConfig = KafkaSingleton.Instance.ConsumerConfig;

    // 生产单条消息
    public async Task ProduceAsync(string topic, string key, string value)
    {
        using var producer = new ProducerBuilder<string, string>(_producerConfig).Build();

        try
        {
            var result = await producer.ProduceAsync(topic, new Message<string, string>
            {
                Key = key,
                Value = value
            });

            Console.WriteLine($"Message sent to {result.TopicPartitionOffset}");
        }
        catch (ProduceException<string, string> e)
        {
            Console.WriteLine($"Failed to deliver message: {e.Error.Reason}");
            throw;
        }
    }

    // 消费消息（带回调处理）
    public void Consume(string topic, Action<string, string> messageHandler, CancellationToken cancellationToken)
    {
        using var consumer = new ConsumerBuilder<string, string>(_consumerConfig).Build();

        consumer.Subscribe(topic);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(cancellationToken);

                    // 通过回调处理消息
                    messageHandler(consumeResult.Message.Key, consumeResult.Message.Value);
                }
                catch (ConsumeException e)
                {
                    Console.WriteLine($"Error occurred: {e.Error.Reason}");
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }

    // 批量生产消息
    public async Task ProduceManyAsync(string topic, IEnumerable<KeyValuePair<string, string>> messages)
    {
        using var producer = new ProducerBuilder<string, string>(_producerConfig).Build();

        var tasks = new List<Task>();
        foreach (var message in messages)
        {
            tasks.Add(producer.ProduceAsync(topic, new Message<string, string>
            {
                Key = message.Key,
                Value = message.Value
            }));
        }

        await Task.WhenAll(tasks);
    }

    // 消费消息并反序列化为指定类型
    public void ConsumeAndDeserialize<TDeserialized>(
        string topic,
        Action<string, TDeserialized> messageHandler,
        CancellationToken cancellationToken)
    {
        using var consumer = new ConsumerBuilder<string, string>(_consumerConfig).Build();

        consumer.Subscribe(topic);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(cancellationToken);

                    // 假设消息值是 JSON 格式，进行反序列化
                    var deserializedValue = JsonSerializer.Deserialize<TDeserialized>(consumeResult.Message.Value);
                    if (deserializedValue != null)
                    {
                        messageHandler(consumeResult.Message.Key, deserializedValue);
                    }
                }
                catch (ConsumeException e)
                {
                    Console.WriteLine($"Error occurred: {e.Error.Reason}");
                }
                catch (JsonException e)
                {
                    Console.WriteLine($"JSON Deserialization error: {e.Message}");
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }
}