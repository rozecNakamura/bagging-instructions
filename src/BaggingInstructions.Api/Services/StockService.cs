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

    /// <summary>指定品目の現在在庫数を取得（全倉庫の quantityonhand 合計）。stock は itemcode で item と紐づく。</summary>
    public async Task<decimal> GetItemStockByItemCodeAsync(string? itemCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(itemCode)) return 0;
        var code = itemCode.Trim();
        var total = await _db.Stocks
            .AsNoTracking()
            .Where(s => s.ItemCd == code)
            .SumAsync(s => s.QuantityOnHand, ct);
        return total;
    }
}
