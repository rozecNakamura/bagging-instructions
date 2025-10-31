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
- **バックエンド**: FastAPI + SQLAlchemy 2.0
- **フロントエンド**: Vanilla JavaScript
- **DB**: PostgreSQL（既存DB使用）

## セットアップ

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
`.env`ファイルを作成し、以下を設定：
```
DATABASE_URL=postgresql://user:password@localhost:5432/dbname
ENVIRONMENT=development
```

### 4. サーバー起動
```bash
uvicorn app.main:app --reload --host 0.0.0.0 --port 8000
```

### 5. ブラウザでアクセス
```
http://localhost:8000/static/index.html
```

## API仕様

### 検索API
- **GET** `/api/search`
- パラメータ: `production_date`, `product_code`
- レスポンス: 受注明細リスト

### 計算API
- **POST** `/api/bagging/calculate`
- Body: `{ "jobord_ids": [1,2,3], "print_type": "instruction" }`
- レスポンス: 袋詰指示書 or ラベルデータ

## ディレクトリ構造
```
bagging-instructions/
├── app/              # バックエンド
│   ├── api/         # APIエンドポイント
│   ├── core/        # 設定・DB
│   ├── models/      # ORMモデル
│   ├── repositories/ # DB操作
│   ├── schemas/     # Pydanticスキーマ
│   └── services/    # ビジネスロジック
├── static/          # フロントエンド
│   ├── js/         # JavaScript
│   └── css/        # CSS
└── requirements.txt
```

## 開発者向け
- SQLAlchemy 2.0を使用
- 既存DBのため、マイグレーションは不要
- ログは `log/` ディレクトリに出力

## ライセンス
内部利用のみ

