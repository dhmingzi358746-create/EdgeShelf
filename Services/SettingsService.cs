using System.Text.Json;

namespace EdgeShelf.Services;

public sealed class EdgeShelfSettings
{
    public int RetentionHours { get; set; } = 24;
    public bool StartWithWindows { get; set; }
    public string Language { get; set; } = "zh-CN";
}

public sealed class SettingsService
{
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.General) { WriteIndented = true };
    public string SettingsPath { get; }

    public SettingsService()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EdgeShelf");
        Directory.CreateDirectory(root);
        SettingsPath = Path.Combine(root, "settings.json");
    }

    public EdgeShelfSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                return JsonSerializer.Deserialize<EdgeShelfSettings>(File.ReadAllText(SettingsPath), _options) ?? new EdgeShelfSettings();
            }
        }
        catch (JsonException)
        {
        }

        return new EdgeShelfSettings();
    }

    public void Save(EdgeShelfSettings settings) => File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, _options));
}
