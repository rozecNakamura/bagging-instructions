-- 作業前準備書・カット前準備書の検索/出力を高速化するためのインデックス追加
--
-- 背景:
--   両画面の SQL には「同一 productno（同一実効日付）は最新 ordertableid のみ採用」する
--   重複排除(DEDUP)の相関サブクエリがあり、ordertable の1行ごとに ordertable 全体を
--   全件スキャンしていた（O(N^2)）。データ増加に伴い検索が2〜3分かかる原因。
--   さらに TRIM(productno) を条件に使うため通常インデックスでは対応できない。
--
-- 対策:
--   (1) TRIM(productno) の関数インデックス   … DEDUP サブクエリを全件スキャン→インデックス検索へ
--   (2) parentordertableid のインデックス     … カット前準備書の「兄弟受注からの製造便継承」
--                                               サブクエリ(s.parentordertableid = ot.parentordertableid)を高速化
--
-- 安全性:
--   インデックス追加のみ。データ・集計結果・帳票内容は一切変わらない。DROP INDEX で元に戻せる。
--   本番では CONCURRENTLY 版（末尾のコメント参照）を推奨（テーブルロックを避けられる）。

-- (1) DEDUP 相関サブクエリ用: TRIM(productno)
CREATE INDEX IF NOT EXISTS idx_ordertable_productno_trim
  ON ordertable (TRIM(BOTH FROM productno));

-- (2) 兄弟受注の製造便継承サブクエリ用: parentordertableid
CREATE INDEX IF NOT EXISTS idx_ordertable_parentordertableid
  ON ordertable (parentordertableid);

-- プランナに最新統計を反映
ANALYZE ordertable;

-- ------------------------------------------------------------------
-- 本番稼働中に無停止で貼る場合は、上記2行の代わりに以下を（トランザクション外で）実行:
--   CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_ordertable_productno_trim
--     ON ordertable (TRIM(BOTH FROM productno));
--   CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_ordertable_parentordertableid
--     ON ordertable (parentordertableid);
--   ANALYZE ordertable;
-- ------------------------------------------------------------------
