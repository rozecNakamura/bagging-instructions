-- 汁仕分表テスト用: salesorder / salesorderline / salesorderlineaddinfo に 25 行追加
-- 条件: customerid=1, customerdeliverylocationid=4〜28, itemid=1, 喫食日 2026-03-04 朝
-- 実行前にシーケンスを同期する場合は、既存データがある場合に主キー重複を防ぐため
-- 以下を必要に応じて実行してください:
--   SELECT setval('salesorder_salesorderid_seq', (SELECT COALESCE(MAX(salesorderid), 0) FROM salesorder));
--   SELECT setval('salesorderline_salesorderlineid_seq', (SELECT COALESCE(MAX(salesorderlineid), 0) FROM salesorderline));
--   SELECT setval('salesorderlineaddinfo_salesorderlineaddinfoid_seq', (SELECT COALESCE(MAX(salesorderlineaddinfoid), 0) FROM salesorderlineaddinfo));

WITH orders AS (
    INSERT INTO salesorder (customerid, customerdeliverylocationid, orderdate, status)
    VALUES
        (1, 4,  '2026-03-04'::timestamp, 'draft'),
        (1, 5,  '2026-03-04'::timestamp, 'draft'),
        (1, 6,  '2026-03-04'::timestamp, 'draft'),
        (1, 7,  '2026-03-04'::timestamp, 'draft'),
        (1, 8,  '2026-03-04'::timestamp, 'draft'),
        (1, 9,  '2026-03-04'::timestamp, 'draft'),
        (1, 10, '2026-03-04'::timestamp, 'draft'),
        (1, 11, '2026-03-04'::timestamp, 'draft'),
        (1, 12, '2026-03-04'::timestamp, 'draft'),
        (1, 13, '2026-03-04'::timestamp, 'draft'),
        (1, 14, '2026-03-04'::timestamp, 'draft'),
        (1, 15, '2026-03-04'::timestamp, 'draft'),
        (1, 16, '2026-03-04'::timestamp, 'draft'),
        (1, 17, '2026-03-04'::timestamp, 'draft'),
        (1, 18, '2026-03-04'::timestamp, 'draft'),
        (1, 19, '2026-03-04'::timestamp, 'draft'),
        (1, 20, '2026-03-04'::timestamp, 'draft'),
        (1, 21, '2026-03-04'::timestamp, 'draft'),
        (1, 22, '2026-03-04'::timestamp, 'draft'),
        (1, 23, '2026-03-04'::timestamp, 'draft'),
        (1, 24, '2026-03-04'::timestamp, 'draft'),
        (1, 25, '2026-03-04'::timestamp, 'draft'),
        (1, 26, '2026-03-04'::timestamp, 'draft'),
        (1, 27, '2026-03-04'::timestamp, 'draft'),
        (1, 28, '2026-03-04'::timestamp, 'draft')
    RETURNING salesorderid
),
lines AS (
    INSERT INTO salesorderline (salesorderid, lineno, itemid, quantity, planneddeliverydate, productdate, status)
    SELECT salesorderid, 1, 1, 1, '2026-03-04'::timestamp, '2026-03-04'::timestamp, 'open'
    FROM orders
    RETURNING salesorderlineid
)
INSERT INTO salesorderlineaddinfo (salesorderlineid, addinfo01, addinfo01name, addinfo02)
SELECT salesorderlineid, '朝', '朝', '1'
FROM lines;
