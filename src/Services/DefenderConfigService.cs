using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2_Defender.Configuration;
using SwiftlyS2_Defender.Interfaces;
using SwiftlyS2_Defender.Models;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SwiftlyS2_Defender.Services;

public sealed class DefenderConfigService : IDefenderConfigService
{
    private readonly ISwiftlyCore _core;
    private readonly ILogger _logger;
    private DefenderConfig _config = new();
    private readonly string _configPath;
    private readonly string _scenariosPath;

    public DefenderConfig Config => _config;

    public DefenderConfigService(ISwiftlyCore core, ILogger logger)
    {
        _core = core;
        _logger = logger;
        
        _configPath = _core.Configuration.GetConfigPath("config.json");
        var baseDir = Path.GetDirectoryName(_configPath)!;
        _scenariosPath = Path.Combine(baseDir, "Scenarios");
    }

    public void LoadOrCreate()
    {
        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (dir is not null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (!Directory.Exists(_scenariosPath))
            {
                Directory.CreateDirectory(_scenariosPath);
            }

            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                _config = JsonSerializer.Deserialize<DefenderConfig>(json) ?? new DefenderConfig();
            }
            else
            {
                var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load or create Defender config.");
        }
    }

    public Scenario? LoadScenario(string name)
    {
        try
        {
            var file = Path.Combine(_scenariosPath, $"{name}.json");
            if (!File.Exists(file)) return null;

            var json = File.ReadAllText(file);
            return JsonSerializer.Deserialize<Scenario>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load scenario {Name}", name);
            return null;
        }
    }

    public void SaveScenario(Scenario scenario)
    {
        try
        {
            var file = Path.Combine(_scenariosPath, $"{scenario.Name}.json");
            var json = JsonSerializer.Serialize(scenario, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(file, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save scenario {Name}", scenario.Name);
        }
    }

    public IEnumerable<string> GetAvailableScenarios()
    {
        if (!Directory.Exists(_scenariosPath)) yield break;

        foreach (var file in Directory.GetFiles(_scenariosPath, "*.json"))
        {
            yield return Path.GetFileNameWithoutExtension(file);
        }
    }
}
