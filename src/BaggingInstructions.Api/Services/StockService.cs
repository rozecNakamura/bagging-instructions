using Microsoft.EntityFrameworkCore;
using BaggingInstructions.Api.Core;

namespace BaggingInstructions.Api.Services;

public class StockService
{
    private readonly AppDbContext _db;

    public StockService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>指定品目の現在在庫数を取得（全倉庫の quantityonhand 合計）。新DBでは日付条件なし。</summary>
    public async Task<decimal> GetItemStockByItemIdAsync(long? itemId, CancellationToken ct = default)
    {
        if (itemId == null) return 0;
        var total = await _db.Stocks
            .AsNoTracking()
            .Where(s => s.ItemId == itemId.Value)
            .SumAsync(s => s.QuantityOnHand, ct);
        return total;
    }
}
