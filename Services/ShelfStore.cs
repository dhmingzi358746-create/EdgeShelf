using System.Collections.ObjectModel;
using System.Text.Json;
using EdgeShelf.Models;

namespace EdgeShelf.Services;

public sealed class ShelfStore
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    public string RootDirectory { get; }
    public string CacheDirectory { get; }
    public string DataFilePath { get; }

    public ShelfStore()
    {
        RootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EdgeShelf");
        CacheDirectory = Path.Combine(RootDirectory, "cache");
        DataFilePath = Path.Combine(RootDirectory, "items.json");
        Directory.CreateDirectory(CacheDirectory);
    }

    public ObservableCollection<ShelfItem> Load()
    {
        if (!File.Exists(DataFilePath))
        {
            return new ObservableCollection<ShelfItem>();
        }

        try
        {
            var json = File.ReadAllText(DataFilePath);
            var items = JsonSerializer.Deserialize<List<ShelfItem>>(json, _jsonOptions) ?? [];
            foreach (var item in items)
            {
                item.RefreshDerivedState();
            }

            return new ObservableCollection<ShelfItem>(items);
        }
        catch (JsonException)
        {
            return new ObservableCollection<ShelfItem>();
        }
    }

    public void Save(IEnumerable<ShelfItem> items)
    {
        Directory.CreateDirectory(RootDirectory);
        var json = JsonSerializer.Serialize(items, _jsonOptions);
        File.WriteAllText(DataFilePath, json);
    }

    public string CreateScreenshotPath()
    {
        var fileName = $"screenshot-{DateTime.Now:yyyyMMdd-HHmmss-fff}.png";
        return Path.Combine(CacheDirectory, fileName);
    }
}
