using PdfSharp.Fonts;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// PdfSharp 用フォント解決。ＭＳ ゴシックなど日本語フォントを提供する。
/// アプリの .ttf → Windows の TTC から第1フォントを抽出 → .ttf の順で使用（PdfSharp は TTC 非対応のため抽出する）。
/// </summary>
public class JuicePdfFontResolver : IFontResolver
{
    public const string JapaneseFaceName = "JuicePdfJapanese";

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        return new FontResolverInfo(JapaneseFaceName, false, false);
    }

    public byte[]? GetFont(string faceName)
    {
        if (faceName != JapaneseFaceName) return null;
        return LoadFontBytes();
    }

    /// <summary>
    /// 日本語フォントのバイト列を取得。.ttf または TTC の第1フォント（PdfSharp は TTC 非対応のため抽出）を使用。
    /// </summary>
    private static byte[] LoadFontBytes()
    {
        // 1) アプリ実行ディレクトリの Fonts フォルダ（.ttf のみ）
        var baseDir = AppContext.BaseDirectory;
        var appFonts = new[]
        {
            Path.Combine(baseDir, "Fonts", "JapaneseFont.ttf"),
            Path.Combine(baseDir, "Fonts", "ipag.ttf"),
            Path.Combine(baseDir, "Fonts", "ipaexg.ttf"),
        };
        foreach (var path in appFonts)
        {
            if (File.Exists(path))
                return File.ReadAllBytes(path);
        }

        // 2) 埋め込みリソースの .ttf（日本語フォント名を優先、なければ任意の .ttf）
        var asm = typeof(JuicePdfFontResolver).Assembly;
        var allTtf = asm.GetManifestResourceNames()
            .Where(n => n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var resourceName = allTtf.FirstOrDefault(n =>
                n.EndsWith("JapaneseFont.ttf", StringComparison.OrdinalIgnoreCase)
                || n.EndsWith("ipaexg.ttf", StringComparison.OrdinalIgnoreCase)
                || n.EndsWith("ipag.ttf", StringComparison.OrdinalIgnoreCase))
            ?? allTtf.FirstOrDefault();
        if (resourceName != null)
        {
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                var bytes = new byte[stream.Length];
                _ = stream.Read(bytes, 0, bytes.Length);
                return bytes;
            }
        }

        // 3) Windows の TTC から第1フォントを抽出（日本語フォント優先）
        if (OperatingSystem.IsWindows())
        {
            var fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            if (!string.IsNullOrEmpty(fontsDir))
            {
                var ttcCandidates = new[]
                {
                    Path.Combine(fontsDir, "msgothic.ttc"),
                    Path.Combine(fontsDir, "meiryo.ttc"),
                    Path.Combine(fontsDir, "yugothm.ttc"),
                };
                foreach (var path in ttcCandidates)
                {
                    if (!File.Exists(path)) continue;
                    try
                    {
                        var ttcBytes = File.ReadAllBytes(path);
                        if (TryExtractFirstFontFromTtc(ttcBytes, out var ttfBytes))
                            return ttfBytes;
                    }
                    catch { /* 次の候補へ */ }
                }
            }

            // 4) Windows の日本語 .ttf（Arial は日本語非対応のため使用しない）
            if (!string.IsNullOrEmpty(fontsDir))
            {
                var path = Path.Combine(fontsDir, "yugothui.ttf"); // Yu Gothic UI（日本語対応）
                if (File.Exists(path))
                    return File.ReadAllBytes(path);
            }
        }

        throw new InvalidOperationException(
            "汁仕分表 PDF 用の日本語フォントが見つかりません。プロジェクトの Fonts フォルダに IPAexゴシック(ipaexg.ttf) などの .ttf を配置するか、Windows の C:\\Windows\\Fonts に yugothui.ttf がある環境で実行してください。Fonts フォルダの README を参照してください。");
    }

    /// <summary>
    /// TTC ファイルの先頭から第1フォント分のバイトを切り出して返す。PdfSharp は TTC 非対応のため単体 TTF として渡す。
    /// </summary>
    private static bool TryExtractFirstFontFromTtc(byte[] ttcBytes, out byte[] ttfBytes)
    {
        ttfBytes = Array.Empty<byte>();
        if (ttcBytes == null || ttcBytes.Length < 16) return false;
        // TTC ヘッダ: "ttcf" (4), version (4), numFonts (4), offsetTable[0] (4) ...
        if (ttcBytes[0] != 't' || ttcBytes[1] != 't' || ttcBytes[2] != 'c' || ttcBytes[3] != 'f')
            return false;
        uint numFonts = ReadUInt32(ttcBytes, 8);
        if (numFonts == 0) return false;
        uint firstOffset = ReadUInt32(ttcBytes, 12);
        if (firstOffset >= (uint)ttcBytes.Length) return false;
        // 第2フォントのオフセットがあればその手前まで、なければファイル末尾までをコピー
        uint endOffset = (uint)ttcBytes.Length;
        if (numFonts > 1 && ttcBytes.Length >= 16)
        {
            uint secondOffset = ReadUInt32(ttcBytes, 16);
            if (secondOffset > firstOffset && secondOffset <= (uint)ttcBytes.Length)
                endOffset = secondOffset;
        }
        int length = (int)(endOffset - firstOffset);
        if (length <= 0 || firstOffset + length > ttcBytes.Length) return false;
        ttfBytes = new byte[length];
        Buffer.BlockCopy(ttcBytes, (int)firstOffset, ttfBytes, 0, length);
        return true;
    }

    private static uint ReadUInt32(byte[] bytes, int offset)
    {
        if (offset + 4 > bytes.Length) return 0;
        return (uint)(bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24));
    }
}
