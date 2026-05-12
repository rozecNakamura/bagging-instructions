using Microsoft.AspNetCore.Mvc;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Controllers;

[ApiController]
[Route("api/cut-preparation")]
public class CutPreparationController : ControllerBase
{
    private readonly CutPreparationService _cutPrepService;
    private readonly CutPreparationPdfService _cutPrepPdfService;
    private readonly CutPreparationExcelService _cutPrepExcelService;
    private readonly PreparationWorkService _prepWorkService;
    private readonly ProductLabelPdfService _productLabelPdfService;
    private readonly IWebHostEnvironment _env;

    public CutPreparationController(
        CutPreparationService cutPrepService,
        CutPreparationPdfService cutPrepPdfService,
        CutPreparationExcelService cutPrepExcelService,
        PreparationWorkService prepWorkService,
        ProductLabelPdfService productLabelPdfService,
        IWebHostEnvironment env)
    {
        _cutPrepService = cutPrepService;
        _cutPrepPdfService = cutPrepPdfService;
        _cutPrepExcelService = cutPrepExcelService;
        _prepWorkService = prepWorkService;
        _productLabelPdfService = productLabelPdfService;
        _env = env;
    }

    [HttpGet("workcenters")]
    public async Task<ActionResult<List<PreparationWorkWorkcenterOptionDto>>> ListWorkcenters(CancellationToken ct)
    {
        try
        {
            var list = await _prepWorkService.ListWorkcentersAsync(ct);
            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"作業区マスタ取得エラー: {ex.Message}" });
        }
    }

    [HttpGet("manufacturing-routes")]
    public async Task<ActionResult<List<PreparationWorkManufacturingRouteOptionDto>>> ListManufacturingRoutes(
        [FromQuery] string delvedt,
        CancellationToken ct)
    {
        try
        {
            var list = await _prepWorkService.ListManufacturingRoutesForNeedDateAsync(delvedt, ct);
            return Ok(list);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"製造便一覧取得エラー: {ex.Message}" });
        }
    }

    /// <summary>製造日・製造便・品目コード・作業区で受注明細を検索（日付×製造便グループ）。</summary>
    [HttpGet("search")]
    public async Task<ActionResult<CutPreparationSearchResponseDto>> Search(
        [FromQuery] string delvedt,
        [FromQuery(Name = "manufacturing_route_code")] string[]? manufacturingRouteCode,
        [FromQuery] string? itemcd,
        [FromQuery(Name = "workcenter_id")] long[]? workcenterId,
        CancellationToken ct)
    {
        try
        {
            var mfg = manufacturingRouteCode ?? Array.Empty<string>();
            var wc = workcenterId ?? Array.Empty<long>();
            var groups = await _cutPrepService.SearchGroupsAsync(delvedt, mfg, itemcd, wc, ct);
            return Ok(new CutPreparationSearchResponseDto { Total = groups.Count, Groups = groups });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"検索エラー: {ex.Message}" });
        }
    }

    /// <summary>カット前準備書 PDF（子品目コード50/51のみ・連続重複除去）。</summary>
    [HttpPost("pdf")]
    public async Task<IActionResult> ExportPdf([FromBody] CutPreparationExportRequestDto body, CancellationToken ct)
    {
        if (body?.GroupKeys == null || body.GroupKeys.Count == 0)
            return BadRequest(new { detail = "印刷するグループを選択してください" });

        var templatePath = Path.Combine(AppContentPaths.TemplatesDirectory(_env), "作業前準備書.rxz");
        var fullPath = Path.GetFullPath(templatePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { detail = "作業前準備書テンプレートが見つかりません" });

        try
        {
            var lineIds = await _cutPrepService.ResolveLineIdsAsync(body, body.GroupKeys, ct);
            if (lineIds.Count == 0)
                return BadRequest(new { detail = "該当する受注明細がありません" });

            var pdfLines = await _cutPrepService.BuildPdfLineModelsAsync(lineIds, ct);
            if (pdfLines.Count == 0)
                return BadRequest(new { detail = "先頭2桁が50または51の子品目がありません" });

            var pdfBytes = _cutPrepPdfService.GeneratePdf(fullPath, pdfLines);
            if (pdfBytes.Length == 0)
                return BadRequest(new { detail = "印刷する行がありません" });

            return File(pdfBytes, "application/pdf", "カット前準備書.pdf");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"PDF出力エラー: {ex.Message}" });
        }
    }

    /// <summary>カット前準備書 Excel（子品目コード50/51のみ・連続重複除去）。</summary>
    [HttpPost("excel")]
    public async Task<IActionResult> ExportExcel([FromBody] CutPreparationExportRequestDto body, CancellationToken ct)
    {
        if (body?.GroupKeys == null || body.GroupKeys.Count == 0)
            return BadRequest(new { detail = "出力するグループを選択してください" });

        try
        {
            var lineIds = await _cutPrepService.ResolveLineIdsAsync(body, body.GroupKeys, ct);
            if (lineIds.Count == 0)
                return BadRequest(new { detail = "該当する受注明細がありません" });

            var lines = await _cutPrepService.BuildPdfLineModelsAsync(lineIds, ct);
            if (lines.Count == 0)
                return BadRequest(new { detail = "先頭2桁が50または51の子品目がありません" });

            var bytes = _cutPrepExcelService.Build(lines);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "カット前準備書.xlsx");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"Excel出力エラー: {ex.Message}" });
        }
    }

    /// <summary>現品ラベル PDF（既存の現品票印刷と同じ出力・instruction_type=cut）。</summary>
    [HttpPost("product-label-pdf")]
    public async Task<IActionResult> ExportProductLabelPdf(
        [FromBody] CutPreparationProductLabelRequestDto body,
        CancellationToken ct)
    {
        if (body?.GroupKeys == null || body.GroupKeys.Count == 0)
            return BadRequest(new { detail = "印刷するグループを選択してください" });

        var templatePath = Path.Combine(AppContentPaths.TemplatesDirectory(_env), ProductLabelPdfService.TemplateFileName);
        var fullPath = Path.GetFullPath(templatePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { detail = "現品票テンプレートが見つかりません" });

        try
        {
            var filter = new CutPreparationFilterRequestDto
            {
                Delvedt = body.Delvedt,
                ManufacturingRouteCodes = body.ManufacturingRouteCodes,
                Itemcd = body.Itemcd,
                WorkcenterIds = body.WorkcenterIds
            };
            var lineIds = await _cutPrepService.ResolveLineIdsAsync(filter, body.GroupKeys, ct);
            if (lineIds.Count == 0)
                return BadRequest(new { detail = "該当する受注明細がありません" });

            var labelCount = Math.Max(1, body.LabelCount);
            var instructionType = string.IsNullOrWhiteSpace(body.InstructionType) ? "cut" : body.InstructionType;

            var pdfBytes = await _productLabelPdfService.GeneratePdfAsync(
                fullPath, lineIds, labelCount, instructionType, ct);
            if (pdfBytes.Length == 0)
                return BadRequest(new { detail = "出力対象の現品ラベルがありません" });

            return File(pdfBytes, "application/pdf", "現品ラベル.pdf");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"現品ラベルPDF出力エラー: {ex.Message}" });
        }
    }
}
