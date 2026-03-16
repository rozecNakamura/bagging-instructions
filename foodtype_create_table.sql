-- foodtype テーブル作成
-- カラム: 主キー(id), 食種コード(text), 食種(text)

CREATE TABLE foodtype (
    id SERIAL PRIMARY KEY,
    "食種コード" text NOT NULL,
    "食種" text
);

-- 食種コードの一意制約（任意）
CREATE UNIQUE INDEX idx_foodtype_code ON foodtype ("食種コード");

COMMENT ON TABLE foodtype IS '食種マスタ（食種コード・食種名）';
