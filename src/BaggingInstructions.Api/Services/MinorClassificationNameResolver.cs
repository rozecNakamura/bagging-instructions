using BaggingInstructions.Api.Entities;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 小分類名称を 大分類 &gt; 中分類 &gt; 小分類 の階層で解決する。
/// 小分類コードは上位分類の中でのみ一意な運用のため、コード単独で引くと
/// 別階層の同一コードを拾う可能性がある。
/// </summary>
public sealed class MinorClassificationNameResolver
{
    private readonly Dictionary<(string Major, string Middle, string Minor), string> _byHierarchy;

    /// <summary>小分類コード単独で名称が一意に決まる場合のみ名称、複数候補がある場合は null。</summary>
    private readonly Dictionary<string, string?> _byMinorCodeOnly;

    public MinorClassificationNameResolver(IEnumerable<MinorClassification> rows)
    {
        _byHierarchy = new Dictionary<(string, string, string), string>();
        _byMinorCodeOnly = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            var minor = Norm(row.MinorClassificationCode);
            if (minor.Length == 0) continue;

            var name = Norm(row.MinorClassificationName);
            _byHierarchy.TryAdd((Norm(row.MajorClassificationCode), Norm(row.MiddleClassificationCode), minor), name);

            if (_byMinorCodeOnly.TryGetValue(minor, out var existing))
            {
                // 別階層に同一コードがあり名称も異なる → コード単独では特定できない
                if (!string.Equals(existing, name, StringComparison.Ordinal))
                    _byMinorCodeOnly[minor] = null;
            }
            else
            {
                _byMinorCodeOnly[minor] = name;
            }
        }
    }

    public static MinorClassificationNameResolver Empty { get; } =
        new(Array.Empty<MinorClassification>());

    /// <summary>品目の大分類・中分類・小分類コードから小分類名称を取得する。特定できない場合は空文字。</summary>
    public string Resolve(Item? item) =>
        Resolve(item?.MajorClassificationCode, item?.MiddleClassificationCode, item?.MinorClassificationCode);

    public string Resolve(string? majorCode, string? middleCode, string? minorCode)
    {
        var minor = Norm(minorCode);
        if (minor.Length == 0) return "";

        if (_byHierarchy.TryGetValue((Norm(majorCode), Norm(middleCode), minor), out var name))
            return name;

        // 品目側に上位分類が未設定などで階層一致しない場合は、
        // 小分類コードから名称が一意に決まるときのみ採用する（曖昧なら出力しない）。
        return _byMinorCodeOnly.TryGetValue(minor, out var unique) && unique != null ? unique : "";
    }

    private static string Norm(string? s) => (s ?? "").Trim();
}
