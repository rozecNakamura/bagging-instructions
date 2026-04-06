namespace BaggingInstructions.Api;

/// <summary>
/// POST /api/production-instruction/report の帳票種別・テンプレート・ダウンロード名。
/// </summary>
internal static class ProductionInstructionReportKinds
{
    internal const string VariantChomi = "chomi";
    internal const string VariantHoikolo = "hoikolo";

    internal const string ChomiTemplateFileName = "調味液配合表.rxz";
    internal const string HoikoloTemplateFileName = "生産指示書_ホイコーロー.rxz";

    internal const string ChomiDownloadFileName = "調味液配合表.pdf";
    internal const string HoikoloDownloadFileName = "生産指示書_ホイコーロー.pdf";

    internal const string HoikoloPdfDocumentTitle = "生産指示書_ホイコーロー";

    internal static bool IsHoikolo(string normalizedVariant) =>
        normalizedVariant == VariantHoikolo;

    internal static bool IsInvalidVariant(string normalizedVariant) =>
        normalizedVariant.Length > 0
        && normalizedVariant != VariantChomi
        && normalizedVariant != VariantHoikolo;
}
