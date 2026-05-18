using Microsoft.AspNetCore.Mvc;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.Services;
using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Controllers;

[ApiController]
[Route("api/bagging")]
public class BaggingController : ControllerBase
{
    private readonly BaggingCalculatorService _baggingCalculator;
    private readonly BaggingInputService _baggingInputService;
    private readonly BaggingLabelPdfService _baggingLabelPdf;
    private readonly BaggingPreparationExcelService _baggingPrepExcel;
    private readonly IWebHostEnvironment _env;

    public BaggingController(
        BaggingCalculatorService baggingCalculator,
        BaggingInputService baggingInputService,
        BaggingLabelPdfService baggingLabelPdf,
        BaggingPreparationExcelService baggingPrepExcel,
        IWebHostEnvironment env)
    {
        _baggingCalculator = baggingCalculator;
        _baggingInputService = baggingInputService;
        _baggingLabelPdf = baggingLabelPdf;
        _baggingPrepExcel = baggingPrepExcel;
        _env = env;
    }

    /// <summary>登録済み投入量を取得。</summary>
    [HttpGet("input")]
    public async Task<ActionResult<BaggingInputGetResponseDto>> GetInput(
        [FromQuery] string prddt,
        [FromQuery] string itemcd,
        [FromQuery(Name = "jobord_prkeys")] List<long>? jobordPrkeys,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(prddt) || string.IsNullOrWhiteSpace(itemcd))
            return BadRequest(new { detail = "prddt と itemcd を指定してください。" });
        var r = await _baggingInputService.GetAsync(prddt.Trim(), itemcd.Trim(), jobordPrkeys, ct);
        return r == null ? BadRequest(new { detail = "製造日の形式が不正です。" }) : Ok(r);
    }

    /// <summary>
    /// 投入量を登録（UPSERT）。
    /// <c>jobord_prkeys</c> が1件以上ある場合は craftlineaxother の <c>baggedquantity</c> へ保存（袋詰画面の通常動作）。
    /// キーなしの場合のみ AppDb の JSON 登録へ保存する。
    /// </summary>
    [HttpPut("input")]
    public async Task<IActionResult> SaveInput([FromBody] BaggingInputSaveRequestDto body, CancellationToken ct)
    {
        try
        {
            await _baggingInputService.SaveAsync(body, ct);
            return Ok(new { ok = true });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    /// <summary>必要量セット用：受注合計に対する BOM 既定総数量。</summary>
    [HttpPost("required-quantities")]
    public async Task<ActionResult<BaggingRequiredQuantitiesResponseDto>> RequiredQuantities(
        [FromBody] BaggingRequiredQuantitiesRequestDto body,
        CancellationToken ct)
    {
        if (body.JobordPrkeys == null || body.JobordPrkeys.Count == 0)
            return BadRequest(new { detail = "jobord_prkeys を指定してください。" });
        var r = await _baggingCalculator.GetRequiredQuantitiesAsync(body.JobordPrkeys, ct);
        return Ok(r);
    }

    /// <summary>印刷済みとしてマーク（製造日・品目コードで UPSERT）。</summary>
    [HttpPost("mark-printed")]
    public async Task<IActionResult> MarkPrinted([FromBody] MarkPrintedRequestDto body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Prddt) || string.IsNullOrWhiteSpace(body.Itemcd))
            return BadRequest(new { detail = "prddt と itemcd を指定してください。" });
        await _baggingInputService.MarkPrintedAsync(body.Prddt.Trim(), body.Itemcd.Trim(), body.PrintType, ct);
        return Ok(new { ok = true });
    }

    /// <summary>袋詰指示書またはラベルデータを計算。ラベルは殺菌温度（strtemp）を含む。</summary>
    [HttpPost("calculate")]
    public async Task<IActionResult> Calculate(
        [FromBody] CalculateRequestDto request,
        CancellationToken ct)
    {
        try
        {
            var calc = await _baggingCalculator.CalculateFullAsync(request, ct);
            var baggingItems = calc.Items;

            if (request.PrintType == "instruction")
                return Ok(new BaggingInstructionResponseDto
                {
                    Items = baggingItems,
                    IngredientDisplayRows = calc.IngredientDisplayRows
                });

            var labelItems = new List<LabelItemDto>();
            foreach (var item in baggingItems)
            {
                var fillQty = calc.EffectiveSpecFillQty ?? BaggingDivisorResolver.ResolveFromItemDetail(item.Item);
                var st = item.Item?.Strtemp ?? item.Item?.Steritemprange;
                var effectiveExpiry = !string.IsNullOrEmpty(request.ExpiryDateOverride)
                    ? request.ExpiryDateOverride
                    : LabelGeneratorService.CalculateExpiryDate(
                        item.Prddt ?? item.Delvedt,
                        item.Item?.ShelflifeDays ?? LabelGeneratorService.DefaultDaysAfter);
                var unitName = item.Item?.Uni?.Uninm ?? item.Item?.Uni?.Uniinfnm;
                var labelStdBags = item.StandardBags;
                var labelIrregular = item.IrregularQuantity;
                var pageNo = labelStdBags + (labelIrregular > 0 ? 1 : 0);
                var shptmName = item.ShptmName;
                var classification1Name = item.Item?.Classification1Name;
                labelItems.AddRange(LabelGeneratorService.GenerateStandardLabelsFromDto(
                    item.Itemcd, item.Itemnm, st, item.Item?.Kikunip, item.Delvedt, item.Shptm, labelStdBags, fillQty, effectiveExpiry, unitName,
                    shptmName: shptmName, shpctrnm: item.Shpctrnm, classification1Name: classification1Name, pageNo: pageNo, startPageNo: 1));
                labelItems.AddRange(LabelGeneratorService.GenerateIrregularLabelsFromDto(
                    item.Itemcd, item.Itemnm, st, item.Delvedt, item.Shptm, item.Shpctrnm, labelIrregular, effectiveExpiry, unitName,
                    shptmName: shptmName, classification1Name: classification1Name, pageNo: pageNo, startPageNo: labelStdBags + 1));
            }
            return Ok(new LabelResponseDto { Items = labelItems });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"計算エラー: {ex.Message}" });
        }
    }

    /// <summary>作業前準備書貼付け Excel を生成（POST）。</summary>
    [HttpPost("preparation-excel")]
    public async Task<IActionResult> DownloadPreparationExcel(
        [FromBody] BaggingPreparationExcelRequestDto body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Prddt))
            return BadRequest(new { detail = "prddt を指定してください。" });
        if (body.JobordPrkeys == null || body.JobordPrkeys.Count == 0)
            return BadRequest(new { detail = "jobord_prkeys を指定してください。" });
        try
        {
            var bytes = await _baggingPrepExcel.BuildAsync(body.JobordPrkeys, body.Prddt.Trim(), body.Aggregate, ct);
            var fileName = $"作業前準備書貼付け_{body.Prddt}.xlsx";
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    /// <summary>袋詰現品票1枚.rxz で PDF 生成。ordertable 不要、LabelItemDto[] を直接受け取る。</summary>
    [HttpPost("label-pdf")]
    public IActionResult GenerateLabelPdf([FromBody] LabelPdfRequestDto? body)
    {
        if (body?.Items == null || body.Items.Count == 0)
            return BadRequest(new { detail = "ラベルデータを指定してください" });

        var templatePath = Path.Combine(AppContentPaths.TemplatesDirectory(_env), ProductLabelPdfService.BaggingTemplateFileName);
        var fullPath = Path.GetFullPath(templatePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { detail = "袋詰現品票1枚テンプレートが見つかりません" });

        try
        {
            var pdfBytes = _baggingLabelPdf.GeneratePdf(fullPath, body.Items);
            if (pdfBytes.Length == 0)
                return BadRequest(new { detail = "出力対象のラベルデータがありません" });
            return File(pdfBytes, "application/pdf", "袋詰現品票1枚.pdf");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"PDF出力エラー: {ex.Message}" });
        }
    }
}
