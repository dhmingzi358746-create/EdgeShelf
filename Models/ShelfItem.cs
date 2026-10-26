using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using EdgeShelf.Services;

namespace EdgeShelf.Models;

public enum ShelfItemKind
{
    Screenshot,
    File
}

public sealed class ShelfItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isPinned;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ShelfItemKind Kind { get; set; }
    public string Path { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresUtc { get; set; }

    public bool IsPinned
    {
        get => _isPinned;
        set => SetField(ref _isPinned, value);
    }

    [JsonIgnore]
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    [JsonIgnore]
    public bool IsScreenshot => Kind == ShelfItemKind.Screenshot;

    [JsonIgnore]
    public string TypeLabel => AppLanguage.Text(IsScreenshot ? "screenshot" : "file");

    [JsonIgnore]
    public bool Exists => File.Exists(Path) || Directory.Exists(Path);

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshDerivedState()
    {
        OnPropertyChanged(nameof(Exists));
        OnPropertyChanged(nameof(IsScreenshot));
        OnPropertyChanged(nameof(TypeLabel));
        OnPropertyChanged(nameof(DisplayName));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
