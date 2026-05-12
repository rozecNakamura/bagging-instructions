# 袋詰指示書・ラベル管理システム デプロイ手順書

> 対象環境: Windows Server / IIS + ASP.NET Core Module V2  
> アプリ: ASP.NET Core 9 Web API（PostgreSQL 接続）  
> 本番 URL: `http://54.218.123.254/BaggingInstructions.Api/static/index.html`  
> IIS アプリ名: `BaggingInstructions.Api`（Default Web Site 配下の子アプリ）

---

## 1. 前提条件（初回のみ）

### 1-1. サーバー要件

| 項目 | 必要なもの |
|------|-----------|
| OS | Windows Server 2019 以降（または Windows 10/11） |
| Web サーバー | IIS 10 以降（役割: Web サーバー、アプリケーション開発 > ASP.NET 4.8） |
| ランタイム | [.NET 9.0 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/9) |
| DB | PostgreSQL（craftlineax・craftlineaxother が到達可能なこと） |

### 1-2. IIS の有効化（未設定の場合）

1. 「サーバーマネージャー」→「役割と機能の追加」
2. 役割:「Web サーバー (IIS)」を選択
3. 機能:「.NET Framework 4.8 機能」も選択して完了

### 1-3. .NET 9.0 Hosting Bundle のインストール

1. Microsoft 公式サイトから `dotnet-hosting-9.x.x-win.exe` をダウンロード
2. 管理者権限で実行してインストール
3. インストール後 **IIS を再起動**:

```
iisreset
```

---

## 2. ビルドと発行（開発機で実施）

### 2-1. 発行コマンド

開発機のリポジトリルートで実行:

```powershell
cd C:\Source\bagging-instructions

dotnet publish src\BaggingInstructions.Api\BaggingInstructions.Api.csproj `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -o publish_output
```

完了すると `publish_output\` フォルダに以下が生成される:

```
publish_output\
  BaggingInstructions.Api.dll   ← メイン DLL
  BaggingInstructions.Api.exe   ← 実行ファイル
  web.config                    ← IIS 設定
  appsettings.json
  static\                       ← フロントエンド（HTML/JS/CSS/テンプレート）
  Fonts\                        ← PDF 用フォント
  *.dll 各種依存ライブラリ
```

### 2-2. 発行物をサーバーへ転送

発行物を zip で固めてサーバーへ転送する（例）:

```powershell
# 発行物を zip 圧縮
Compress-Archive -Path publish_output\* -DestinationPath bagging_release.zip
```

サーバー上で展開する場所の例: `C:\inetpub\wwwroot\bagging\`

---

## 3. サーバー側の設定（初回 / 接続先変更時）

### 3-1. appsettings.Production.json の作成

**`appsettings.json` は接続文字列にプレースホルダーが入っているため、本番用設定ファイルを別途作成すること。**

発行先フォルダ（`C:\inetpub\wwwroot\BaggingInstructions.Api\`）に以下のファイルを作成:

**ファイル名: `appsettings.Production.json`**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=<DBサーバーIP>;Port=5432;Database=craftlineax;Username=rozec;Password=<パスワード>",
    "CraftlineaxOther": "Host=<DBサーバーIP>;Port=5432;Database=craftlineaxother;Username=rozec;Password=<パスワード>"
  },
  "Environment": "Production",
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

> `<DBサーバーIP>` と `<パスワード>` を実際の値に置き換える。

### 3-2. ASPNETCORE_ENVIRONMENT の設定

`web.config` の `<environmentVariables>` セクションを編集して本番環境を指定する:

```xml
<environmentVariables>
  <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
</environmentVariables>
```

または IIS 管理コンソールで設定:  
「サイト」→「アプリケーション」→「構成エディター」→ `environmentVariables` に追加

---

## 4. IIS サイト / アプリケーションの設定

### 4-1. アプリケーションプールの作成

1. IIS マネージャーを開く
2. 「アプリケーションプール」→「追加」
3. 以下の設定で作成:

| 項目 | 値 |
|------|---|
| 名前 | `BaggingInstructions` |
| .NET CLR バージョン | **マネージドコードなし** |
| マネージドパイプラインモード | 統合 |

### 4-2. Default Web Site 配下に子アプリとして配置（本番構成）

1. IIS マネージャー →「Default Web Site」→「アプリケーションの追加」
2. 以下の設定:

| 項目 | 値 |
|------|---|
| エイリアス | `BaggingInstructions.Api` |
| アプリケーションプール | `BaggingInstructions` |
| 物理パス | `C:\inetpub\wwwroot\BaggingInstructions.Api` |

3.「アプリケーションの設定」（環境変数）に以下を追加:

| 名前 | 値 |
|------|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_PATHBASE` | `/BaggingInstructions.Api` |

### 4-3. フォルダへのアクセス権付与

IIS ワーカープロセス（`IIS_IUSRS`）に発行フォルダの読み取り権限を付与:

```powershell
icacls "C:\inetpub\wwwroot\BaggingInstructions.Api" /grant "IIS_IUSRS:(OI)(CI)R" /T
```

---

## 5. デプロイ手順（2回目以降の更新）

> 初回設定（セクション 3・4）が完了している前提。

```
1. 開発機でビルド・発行（セクション 2-1）
2. サービス停止（任意）
3. 発行物をサーバーへコピー（appsettings.Production.json は上書きしない）
4. IIS を再起動してサービス再開
5. 動作確認
```

### 5-1. 発行物のコピー（サーバー上で実行）

```powershell
# IIS のプロセスを止める（ファイルロック回避）
Stop-WebSite -Name "Default Web Site"

# 発行物を展開（appsettings.Production.json は上書き除外）
robocopy C:\work\bagging_release C:\inetpub\wwwroot\BaggingInstructions.Api /MIR /XF appsettings.Production.json

# IIS 再起動
Start-WebSite -Name "Default Web Site"
```

---

## 6. 動作確認

### 6-1. ヘルスチェック

```powershell
Invoke-WebRequest -Uri "http://54.218.123.254/BaggingInstructions.Api/health" -UseBasicParsing
```

レスポンス例:
```json
{"status":"ok","environment":"Production"}
```

### 6-2. 画面確認

ブラウザで以下にアクセス:

```
http://54.218.123.254/BaggingInstructions.Api/static/index.html
```

メニューが表示されること、検索機能が動作することを確認する。

### 6-3. ログの確認（問題発生時）

stdout ログを有効にする場合は `web.config` を編集:

```xml
<aspNetCore processPath="dotnet"
            arguments=".\BaggingInstructions.Api.dll"
            stdoutLogEnabled="true"
            stdoutLogFile=".\logs\stdout"
            ... >
```

`logs\` フォルダが存在しない場合は作成する:

```powershell
New-Item -ItemType Directory "C:\inetpub\wwwroot\BaggingInstructions.Api\logs"
```

ログ確認後は `stdoutLogEnabled="false"` に戻すこと（ファイルが肥大化するため）。

---

## 7. ロールバック手順

問題が発生した場合、旧バージョンを `C:\inetpub\wwwroot\BaggingInstructions.Api_bak\` 等に保管しておき、以下で切り戻す:

```powershell
Stop-WebSite -Name "Default Web Site"

robocopy C:\inetpub\wwwroot\BaggingInstructions.Api_bak C:\inetpub\wwwroot\BaggingInstructions.Api /MIR /XF appsettings.Production.json

Start-WebSite -Name "Default Web Site"
```

---

## 8. 注意事項

| 項目 | 内容 |
|------|------|
| `appsettings.Production.json` | **デプロイ時に上書きしない**。接続文字列・パスワードが消える |
| `appsettings.json` の接続文字列 | プレースホルダー（`***`）のまま管理。パスワードをコミットしない |
| IIS アプリケーションプールの .NET バージョン | **「マネージドコードなし」**（.NET Core は ANCM が管理する） |
| PostgreSQL のファイアウォール | サーバーから DB への 5432 ポートが開いていること |
| フォント | `Fonts\` フォルダに .ttf が含まれていること（PDF 出力に必要） |
| 静的ファイル | `static\` フォルダが発行物に含まれていること |
