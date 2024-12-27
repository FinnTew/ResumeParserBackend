namespace ResumeParserBackend;

using System;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public class ES
{
    public string Host { get; set; }
    public int Port { get; set; }
    public string Index { get; set; }
    
    public int Retry { get; set; }
}

public class Mongo 
{
    public string Host { get; set; }
    public int Port { get; set; }
    public string Database { get; set; }
}

public class Rpc
{
    public string Host { get; set; }
    public int Port { get; set; }
}

public class Config {
    public ES Es { get; set; } = new();
    public Mongo Mongo { get; set; } = new();
    public Rpc Rpc { get; set; } = new();
}

public class ConfigManager
{
    private static readonly Lazy<ConfigManager> _instance = new(() => new ConfigManager());
    private Config _config;

    private readonly string _configFilePath = "config.yaml";

    public static ConfigManager Instance => _instance.Value;

    private ConfigManager()
    {
        if (!File.Exists(_configFilePath))
        {
            throw new FileNotFoundException($"Configuration file not found: {_configFilePath}");
        }

        var yaml = File.ReadAllText(_configFilePath);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        _config = deserializer.Deserialize<Config>(yaml);
    }

    public T Get<T>(Func<Config, T> selector)
    {
        return selector(_config);
    }
}