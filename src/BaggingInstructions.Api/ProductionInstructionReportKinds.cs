namespace BaggingInstructions.Api;

/// <summary>
/// POST /api/production-instruction/report の帳票種別・テンプレート・ダウンロード名。
/// </summary>
internal static class ProductionInstructionReportKinds
{
    internal const string VariantChomi = "chomi";
    internal const string VariantHoikolo = "hoikolo";
    internal const string VariantGanmonoTakiai = "ganmono_takiai";
    internal const string VariantCabWinnaSoti = "cab_winna_soti";

    internal const string ChomiTemplateFileName = "調味液配合表.rxz";
    internal const string HoikoloTemplateFileName = "生産指示書_ホイコーロー.rxz";
    internal const string GanmonoTakiaiTemplateFileName = "生産指示書_がんもの炊き合わせ.rxz";
    internal const string CabWinnaSotiTemplateFileName = "生産指示書_キャベツとウィンナーのソティ.rxz";

    internal const string ChomiDownloadFileName = "調味液配合表.pdf";
    internal const string HoikoloDownloadFileName = "生産指示書_ホイコーロー.pdf";
    internal const string GanmonoTakiaiDownloadFileName = "生産指示書_がんもの炊き合わせ.pdf";
    internal const string CabWinnaSotiDownloadFileName = "生産指示書_キャベツとウィンナーのソティ.pdf";

    internal const string HoikoloPdfDocumentTitle = "生産指示書_ホイコーロー";
    internal const string GanmonoTakiaiPdfDocumentTitle = "生産指示書_がんもの炊き合わせ";
    internal const string CabWinnaSotiPdfDocumentTitle = "生産指示書_キャベツとウィンナーのソティ";

    internal const string InvalidVariantDetail =
        "report_variant は省略、'chomi'、'hoikolo'、'ganmono_takiai'、'cab_winna_soti' のいずれかを指定してください。";

    internal static bool IsInvalidVariant(string normalizedVariant) =>
        normalizedVariant.Length > 0
        && normalizedVariant != VariantChomi
        && normalizedVariant != VariantHoikolo
        && normalizedVariant != VariantGanmonoTakiai
        && normalizedVariant != VariantCabWinnaSoti;

    internal static string TemplateFileName(string normalizedVariant) =>
        normalizedVariant switch
        {
            VariantHoikolo => HoikoloTemplateFileName,
            VariantGanmonoTakiai => GanmonoTakiaiTemplateFileName,
            VariantCabWinnaSoti => CabWinnaSotiTemplateFileName,
            _ => ChomiTemplateFileName
        };

    internal static string TemplateMissingMessage(string normalizedVariant) =>
        normalizedVariant switch
        {
            VariantHoikolo => "生産指示書_ホイコーローテンプレートが見つかりません",
            VariantGanmonoTakiai => "生産指示書_がんもの炊き合わせテンプレートが見つかりません",
            VariantCabWinnaSoti => "生産指示書_キャベツとウィンナーのソティテンプレートが見つかりません",
            _ => "調味液配合表テンプレートが見つかりません"
        };

    internal static string DownloadFileName(string normalizedVariant) =>
        normalizedVariant switch
        {
            VariantHoikolo => HoikoloDownloadFileName,
            VariantGanmonoTakiai => GanmonoTakiaiDownloadFileName,
            VariantCabWinnaSoti => CabWinnaSotiDownloadFileName,
            _ => ChomiDownloadFileName
        };
}
