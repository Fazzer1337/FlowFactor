using System.IO;
using System.Text.Json;

namespace FlowFactor.Services;

public static class PresetStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static void Save(string path, PresetDto preset)
    {
        var json = JsonSerializer.Serialize(preset, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static PresetDto Load(string path)
    {
        var json = File.ReadAllText(path);
        var preset = JsonSerializer.Deserialize<PresetDto>(json, JsonOptions);
        return preset ?? new PresetDto();
    }
}