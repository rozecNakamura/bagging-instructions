-- 個人配送指示書「22行超＝ページまたぎ」確認用テストデータ
-- 同じ (配送日, 喫食時間, 配送エリア) に属する SalesOrderLine を 23 件以上にする。
--
-- 前提:
--   - customerid=1, itemid=1 が存在すること
--   - customerdeliverylocation に 23 件以上の納入場所があること（ID 4〜26 を想定。customerdeliverylocation_insert_25.sql で 1〜25 の場所を入れている場合は、ID が deliverylocationid の連番に依存）
--
-- 実行後: 配送日 2026-03-04 で検索 → 「喫食時間 朝」「配送エリア 01」の行を選択 → 個人配送指示書出力で 23 行以上になり、2ページ目にまたがることを確認。
--
-- 【既に juice_test_data_insert.sql を実行している場合】
--   受注・明細は 25 件あるので、以下の「1)」だけ実行し、納入場所に addinfo01='01' を付けると、
--   (2026-03-04, 朝, 01) の 1 行に 25 件がまとまり、出力で 25 行＝2ページになる。「2)」は実行しないこと。

-- 1) 納入場所に「配送エリア 01」を設定（検索結果で「配送エリア 01」にまとめるため）
--    craftlineax: customerdeliverylocationaddinfo は deliverylocationid ではなく (customercode, deliverylocationcode) で紐づく
INSERT INTO customerdeliverylocationaddinfo (customercode, deliverylocationcode, addinfo01)
SELECT d.customercode, d.locationcode, '01'
FROM customerdeliverylocation d
WHERE d.deliverylocationid BETWEEN 4 AND 26
  AND NOT EXISTS (
    SELECT 1 FROM customerdeliverylocationaddinfo a
    WHERE a.customercode IS NOT DISTINCT FROM d.customercode
      AND a.deliverylocationcode IS NOT DISTINCT FROM d.locationcode
  );

-- 2) 【受注・明細がまだ無い場合のみ】同じ (配送日, 喫食時間, 配送エリア) で 23 件の受注＋明細を追加
--    juice_test_data_insert.sql を既に実行している場合はこのブロックは実行しない（重複するため）
WITH orders AS (
    INSERT INTO salesorder (customerid, customerdeliverylocationid, orderdate, status)
    SELECT 1, id, '2026-03-04'::date, 'draft'
    FROM generate_series(4, 26) AS id
    RETURNING salesorderid
),
lines AS (
    INSERT INTO salesorderline (salesorderid, lineno, itemid, quantity, planneddeliverydate, productdate, status)
    SELECT salesorderid, 1, 1, 1, '2026-03-04'::date, '2026-03-04'::date, 'open'
    FROM orders
    RETURNING salesorderlineid
)
INSERT INTO salesorderlineaddinfo (salesorderlineid, addinfo01, addinfo01name, addinfo02, addinfo03name)
SELECT salesorderlineid, '朝', '朝', '1', 'テスト'
FROM lines;
