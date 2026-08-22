//

using System;
using System.IO;
using System.Text.Json;
using AutoHitCounter.Interfaces;
using AutoHitCounter.Models;

namespace AutoHitCounter.Services;

/// <summary>
/// Persists the multirun setup and progress to %AppData%\AutoHitCounter\multirun.json.
/// </summary>
public class MultirunFileStore : IMultirunStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AutoHitCounter",
        "multirun.json");

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public MultirunConfig Load()
    {
        if (!File.Exists(FilePath))
            return MultirunConfig.CreateDefault();

        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<MultirunConfig>(json, ReadOptions) ?? MultirunConfig.CreateDefault();
        }
        catch
        {
            return MultirunConfig.CreateDefault();
        }
    }

    public void Save(MultirunConfig config)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(config, WriteOptions));
        }
        catch (Exception ex)
        {
            Console.WriteLine($@"Error saving multirun settings: {ex.Message}");
        }
    }
}
