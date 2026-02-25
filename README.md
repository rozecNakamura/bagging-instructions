# 袋詰指示書・ラベル管理システム

## 概要
複数施設の受注を一括製造し、完成品を按分して袋詰めする業務を支援するシステムです。

## 機能
- 受注明細検索（製造日・品目コード）
- 袋詰指示書の自動計算
  - 個数単位の切り上げ
  - 完成量の按分
  - 集計ルール適用
- ラベル生成（規格品・端数）
- PDF出力

## 技術スタック
- **バックエンド**: FastAPI + SQLAlchemy 2.0（Python）/ **ASP.NET Core 8 + EF Core**（C#）
- **フロントエンド**: Vanilla JavaScript（static/） / TypeScript + React（frontend/）
- **DB**: PostgreSQL（既存DB使用）

---

## C# 版セットアップ（ASP.NET Core）

### 1. 前提
- .NET 8 SDK をインストール
- PostgreSQL（ROZECDB）が利用可能であること

### 2. 接続文字列
`src/BaggingInstructions.Api/appsettings.Development.json` の `ConnectionStrings:DefaultConnection` を編集するか、環境変数 `ConnectionStrings__DefaultConnection` を設定します。

**新DB（craftlineax）対応後**は、接続先を craftlineax 用のデータベースに変更してください。
```
Host=localhost;Port=5432;Database=<craftlineax DB名>;Username=***;Password=***
```
※ 従来の ROZECDB 用スキーマ（jobord, item, mbom 等）は Legacy に退避済み。現在の API は craftlineax のテーブル（salesorder, salesorderline, item, bom 等）を参照します。

### 3. ビルド・実行
```bash
cd src/BaggingInstructions.Api
dotnet restore
dotnet run
```
- 既定で **http://localhost:8000** で起動（launchSettings.json で変更可）
- Swagger: http://localhost:8000/swagger

### 4. 本番（IIS）
- ASP.NET Core モジュールをインストールし、アプリを発行して IIS でホスト。詳細は .NET の「IIS でホスト」ドキュメントを参照。

### 5. フロントエンド（Vite + React + TypeScript）
C# API と連携する UI は `frontend/` で開発します。開発時は C# API を先に起動し、Vite の proxy で `/api` と `/health` を API に転送します。

```bash
cd frontend
npm install
npm run dev
```
- 開発サーバーは既定で **http://localhost:5173**。ブラウザで開き、製造日・品目で検索し、袋詰計算・印刷が可能です。

本番用ビルド:
```bash
cd frontend
npm run build
```
- 出力は `frontend/dist/`。C# 側の静的ファイル配信（例: wwwroot や `../static`）に `dist` の中身を配置すれば、同一オリジンで API と UI を提供できます。

### C# API 契約（実体）
- 検索: **GET** `/api/search?prddt=YYYYMMDD&itemcd=...`（itemcd は部分一致）
- 詳細: **POST** `/api/search/detail` Body: `{ "prkeys": [1,2,3] }`
- 計算: **POST** `/api/bagging/calculate` Body: `{ "jobord_prkeys": [1,2,3], "print_type": "instruction" }`
- ヘルス: **GET** `/health`

---

## Python 版セットアップ

仮想環境からのセットアップ
### 1. 仮想環境作成
```bash
python -m venv venv
source venv/bin/activate  # Windows: venv\Scripts\activate
```

### 2. パッケージインストール
```bash
pip install -r requirements.txt
```

### 3. 環境変数設定
`.env`ファイルを作成

### 4. サーバー起動
```bash
uvicorn app.main:app --reload --host 0.0.0.0 --port 8000
```

ローカルからのセットアップ
### 1. パッケージインストール
```bash
python -m pip install --user -r requirements.txt
```

### 2. サーバー起動
```bash
python -m uvicorn app.main:app --reload --host 0.0.0.0 --port 8000 
```

### DB接続設定（.env）
1. プロジェクトルートに `.env` を作成し、`app/core/config.py` が読み込む `DATABASE_URL` を定義します。
   ```env
   DATABASE_URL=postgresql://ユーザー名:パスワード@ホスト名またはIP:5432/DB名
   ```
2. `ホスト名またはIP` は接続先 PostgreSQL が動作する場所に合わせて設定します。開発PC上のDBなら `localhost`、別サーバーならそのIP（例: `172.27.128.1`）を指定してください。
3. 接続確認は `uvicorn` 起動時のログ、または `psql` などで同じURLを使って実行し、認証情報に誤りがないか確かめてください。

### ブラウザでアクセス
```
http://localhost:8000/static/index.html
```

## API仕様

### 検索API
- **GET** `/api/search`
- パラメータ: `prddt`（製造日 YYYYMMDD）, `itemcd`（品目コード・部分一致）
- レスポンス: 受注明細リスト

### 計算API
- **POST** `/api/bagging/calculate`
- Body: `{ "jobord_prkeys": [1,2,3], "print_type": "instruction" }` または `"print_type": "label"`
- レスポンス: 袋詰指示書 or ラベルデータ

## ディレクトリ構造
```
bagging-instructions/
├── app/                    # Python バックエンド
│   ├── api/               # APIエンドポイント
│   ├── core/               # 設定・DB
│   ├── models/             # ORMモデル
│   ├── repositories/       # DB操作
│   ├── schemas/            # Pydanticスキーマ
│   └── services/            # ビジネスロジック
├── frontend/               # フロントエンド（Vite + React + TypeScript）
│   ├── src/
│   │   ├── api/            # API クライアント
│   │   ├── components/     # React コンポーネント
│   │   └── types/          # 型定義
│   └── dist/               # ビルド出力（本番配置用）
├── src/
│   └── BaggingInstructions.Api/  # C# バックエンド
│       ├── Controllers/    # API
│       ├── Core/           # DbContext・設定
│       ├── DTOs/           # リクエスト・レスポンス
│       ├── Entities/      # EF Core エンティティ
│       └── Services/       # ビジネスロジック
├── static/                 # 従来フロント（Vanilla JS）
│   ├── js/
│   └── css/
├── BaggingInstructions.sln # C# ソリューション
└── requirements.txt        # Python 依存
```

## 開発者向け
- SQLAlchemy 2.0を使用
- 既存DBのため、マイグレーションは不要
- ログは `log/` ディレクトリに出力

