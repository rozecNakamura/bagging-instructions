-- baggedquantity に指示書・ラベル別の印刷済みフラグを追加
-- 実行後: 既存の isprinted=true 行は両方印刷済みとしてみなす

ALTER TABLE baggedquantity
  ADD COLUMN IF NOT EXISTS isinstructionprinted boolean NOT NULL DEFAULT false,
  ADD COLUMN IF NOT EXISTS islabelprinted boolean NOT NULL DEFAULT false;

-- 既存の印刷済み行を引き継ぎ（両方完了扱い）
UPDATE baggedquantity
SET isinstructionprinted = true,
    islabelprinted       = true
WHERE isprinted = true;
