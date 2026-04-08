using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Entities;

namespace BaggingInstructions.Api.Services;

public class BaggingInputService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AppDbContext _db;
    private readonly CstmeatDbContext _otherDb;

    public BaggingInputService(AppDbContext db, CstmeatDbContext otherDb)
    {
        _db = db;
        _otherDb = otherDb;
    }

    public static DateOnly? ParsePrddt(string? prddt)
    {
        if (string.IsNullOrEmpty(prddt) || prddt.Length != 8) return null;
        if (int.TryParse(prddt.AsSpan(0, 4), out var y) && int.TryParse(prddt.AsSpan(4, 2), out var m) && int.TryParse(prddt.AsSpan(6, 2), out var d))
            return new DateOnly(y, m, d);
        return null;
    }

    public async Task<BaggingInputGetResponseDto?> GetAsync(
        string prddt,
        string itemcd,
        IReadOnlyList<long>? jobordPrkeys,
        CancellationToken ct = default)
    {
        var date = ParsePrddt(prddt);
        if (!date.HasValue) return null;
        var itemTrim = itemcd.Trim();

        if (jobordPrkeys is { Count: > 0 })
        {
            var (payload, updatedAt) = await LoadFromBaggedQuantityAsync(date.Value, itemTrim, ct);
            if (payload != null)
                await MergeParentYieldFromRegistrationAsync(date.Value, itemTrim, payload, ct);
            return new BaggingInputGetResponseDto
            {
                Prddt = prddt,
                Itemcd = itemTrim,
                Payload = payload,
                UpdatedAt = updatedAt
            };
        }

        return await GetFromJsonRegistrationAsync(prddt, itemTrim, date.Value, ct);
    }

    private async Task<BaggingInputGetResponseDto?> GetFromJsonRegistrationAsync(
        string prddt,
        string itemcd,
        DateOnly date,
        CancellationToken ct)
    {
        var row = await _db.BaggingInputRegistrations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ProductDate == date && r.ItemCode == itemcd, ct);
        if (row == null)
        {
            return new BaggingInputGetResponseDto
            {
                Prddt = prddt,
                Itemcd = itemcd,
                Payload = null,
                UpdatedAt = null
            };
        }

        BaggingInputPayloadDto? payload = null;
        try
        {
            payload = JsonSerializer.Deserialize<BaggingInputPayloadDto>(row.Payload, JsonOptions);
        }
        catch
        {
            payload = new BaggingInputPayloadDto();
        }

        return new BaggingInputGetResponseDto
        {
            Prddt = prddt,
            Itemcd = itemcd,
            Payload = payload,
            UpdatedAt = row.UpdatedAt
        };
    }

    private async Task<(BaggingInputPayloadDto? Payload, DateTime? UpdatedAt)> LoadFromBaggedQuantityAsync(
        DateOnly productDate,
        string parentItemCode,
        CancellationToken ct)
    {
        var rows = await _otherDb.BaggedQuantities.AsNoTracking()
            .Where(r => r.ProductDate == productDate && r.ParentItemCode == parentItemCode)
            .OrderBy(r => r.InputOrder)
            .ThenBy(r => r.ChildItemCode)
            .ToListAsync(ct);
        if (rows.Count == 0)
            return (null, null);

        var payload = new BaggingInputPayloadDto
        {
            Lines = rows.Select(r => new BaggingInputLineDto
            {
                Citemcd = r.ChildItemCode,
                InputOrder = r.InputOrder,
                SpecQty = r.StandardQuantity,
                TotalQty = r.TotalQuantity
            }).ToList()
        };
        var updatedAt = rows.Max(r => r.UpdatedAt);
        return (payload, updatedAt);
    }

    public async Task SaveAsync(BaggingInputSaveRequestDto request, CancellationToken ct = default)
    {
        var date = ParsePrddt(request.Prddt)
                   ?? throw new ArgumentException("製造日はYYYYMMDD形式（8桁）で指定してください。", nameof(request.Prddt));
        if (string.IsNullOrWhiteSpace(request.Itemcd))
            throw new ArgumentException("品目コードを指定してください。", nameof(request.Itemcd));

        var itemCodeTrim = request.Itemcd.Trim();
        var keys = request.JobordPrkeys?.Where(k => k != 0).Distinct().ToList();

        var bomChildCodes = await _db.Boms.AsNoTracking()
            .Where(b => b.ParentItemCd == itemCodeTrim)
            .Select(b => b.ChildItemCd ?? "")
            .Where(c => c != "")
            .Distinct()
            .ToListAsync(ct);
        BaggingInputPayloadValidator.ValidateLinesAgainstBom(bomChildCodes, request.Payload);

        if (keys is { Count: > 0 })
        {
            await SaveToBaggedQuantityAsync(date, itemCodeTrim, request.Payload, ct);
            await UpsertParentYieldSidecarAsync(date, itemCodeTrim, request.Payload?.ParentYieldQuantity, ct);
            return;
        }

        var json = JsonSerializer.Serialize(request.Payload ?? new BaggingInputPayloadDto(), JsonOptions);
        var now = DateTime.UtcNow;

        var existing = await _db.BaggingInputRegistrations
            .FirstOrDefaultAsync(r => r.ProductDate == date && r.ItemCode == itemCodeTrim, ct);

        if (existing == null)
        {
            _db.BaggingInputRegistrations.Add(new BaggingInputRegistration
            {
                ProductDate = date,
                ItemCode = itemCodeTrim,
                Payload = json,
                UpdatedAt = now
            });
        }
        else
        {
            existing.Payload = json;
            existing.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 同一 productdate + parentitemcode の既存行を削除し、ペイロードの行を IDENTITY で挿入する。
    /// </summary>
    private async Task SaveToBaggedQuantityAsync(
        DateOnly productDate,
        string parentItemCode,
        BaggingInputPayloadDto? payload,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var lines = payload?.Lines ?? new List<BaggingInputLineDto>();

        var toRemove = await _otherDb.BaggedQuantities
            .Where(r => r.ProductDate == productDate && r.ParentItemCode == parentItemCode)
            .ToListAsync(ct);
        _otherDb.BaggedQuantities.RemoveRange(toRemove);
        await _otherDb.SaveChangesAsync(ct);

        for (var idx = 0; idx < lines.Count; idx++)
        {
            var line = lines[idx];
            var citem = (line.Citemcd ?? "").Trim();
            if (string.IsNullOrEmpty(citem)) continue;

            var inputOrder = line.InputOrder is > 0 ? line.InputOrder!.Value : idx + 1;

            _otherDb.BaggedQuantities.Add(new BaggedQuantity
            {
                ProductDate = productDate,
                ParentItemCode = parentItemCode,
                ChildItemCode = citem,
                InputOrder = inputOrder,
                StandardQuantity = line.SpecQty,
                TotalQuantity = line.TotalQty,
                UpdatedAt = now
            });
        }

        await _otherDb.SaveChangesAsync(ct);
    }

    private async Task MergeParentYieldFromRegistrationAsync(
        DateOnly date,
        string itemTrim,
        BaggingInputPayloadDto payload,
        CancellationToken ct)
    {
        var row = await _db.BaggingInputRegistrations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ProductDate == date && r.ItemCode == itemTrim, ct);
        if (row == null) return;
        try
        {
            var side = JsonSerializer.Deserialize<BaggingInputPayloadDto>(row.Payload, JsonOptions);
            if (side?.ParentYieldQuantity.HasValue == true)
                payload.ParentYieldQuantity = side.ParentYieldQuantity;
        }
        catch
        {
            // ignore malformed sidecar
        }
    }

    /// <summary>jobord 指定保存時、出来高は app 側 JSON テーブルにのみ保持（行は craftlineaxother.baggedquantity）。</summary>
    private async Task UpsertParentYieldSidecarAsync(
        DateOnly date,
        string itemCode,
        decimal? parentYield,
        CancellationToken ct)
    {
        var sidecar = new BaggingInputPayloadDto
        {
            ParentYieldQuantity = parentYield,
            Lines = new List<BaggingInputLineDto>()
        };
        var json = JsonSerializer.Serialize(sidecar, JsonOptions);
        var now = DateTime.UtcNow;
        var existing = await _db.BaggingInputRegistrations
            .FirstOrDefaultAsync(r => r.ProductDate == date && r.ItemCode == itemCode, ct);
        if (existing == null)
        {
            _db.BaggingInputRegistrations.Add(new BaggingInputRegistration
            {
                ProductDate = date,
                ItemCode = itemCode,
                Payload = json,
                UpdatedAt = now
            });
        }
        else
        {
            existing.Payload = json;
            existing.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<BaggingInputPayloadDto?> TryGetPayloadAsync(
        string prddt,
        string itemcd,
        IReadOnlyList<long>? jobordPrkeys = null,
        CancellationToken ct = default)
    {
        var date = ParsePrddt(prddt);
        if (!date.HasValue) return null;

        if (jobordPrkeys is { Count: > 0 })
        {
            var (payload, _) = await LoadFromBaggedQuantityAsync(date.Value, itemcd.Trim(), ct);
            if (payload?.Lines is { Count: > 0 })
            {
                await MergeParentYieldFromRegistrationAsync(date.Value, itemcd.Trim(), payload, ct);
                return payload;
            }
        }

        var row = await _db.BaggingInputRegistrations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ProductDate == date.Value && r.ItemCode == itemcd.Trim(), ct);
        if (row == null) return null;

        try
        {
            return JsonSerializer.Deserialize<BaggingInputPayloadDto>(row.Payload, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
