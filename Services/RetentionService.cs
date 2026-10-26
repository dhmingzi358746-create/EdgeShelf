using EdgeShelf.Models;

namespace EdgeShelf.Services;

public sealed class RetentionService
{
    public int RetentionHours { get; set; } = 24;

    public IEnumerable<ShelfItem> FindExpired(IEnumerable<ShelfItem> items, DateTimeOffset now)
    {
        return items.Where(item =>
            item.Kind == ShelfItemKind.Screenshot &&
            !item.IsPinned &&
            item.ExpiresUtc is not null &&
            item.ExpiresUtc <= now);
    }
}
