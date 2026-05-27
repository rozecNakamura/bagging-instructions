namespace BaggingInstructions.Api.Core;

/// <summary>
/// リポジトリ直下の <c>static</c>（開発時）またはビルド出力内の <c>static</c>（発行・IIS）を解決する。
/// </summary>
public static class AppContentPaths
{
    public static string StaticRoot(IWebHostEnvironment env)
    {
        var devRepoStatic = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..", "static"));

        // 開発時はリポジトリ直下の static を優先（.rxz 編集が bin コピー待ちにならない）
        if (env.IsDevelopment() && Directory.Exists(devRepoStatic))
            return devRepoStatic;

        var besideDll = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "static"));
        if (Directory.Exists(besideDll))
            return besideDll;

        if (Directory.Exists(devRepoStatic))
            return devRepoStatic;

        return besideDll;
    }

    public static string TemplatesDirectory(IWebHostEnvironment env) =>
        Path.Combine(StaticRoot(env), "templates");
}
