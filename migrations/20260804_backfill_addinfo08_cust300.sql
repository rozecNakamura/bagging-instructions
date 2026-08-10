-- ============================================================================
-- 納入場所追加情報 addinfo08 の未設定を補完する（得意先 300 = 在宅個人）
--
-- 背景:
--   弁当箱盛り付け指示書（ご飯）などは納入場所の addinfo08 が「1」始まり
--   （= 個別）であることを条件にしている。得意先 300 の納入場所のうち 25 件は
--   addinfo08 が未設定（NULL、または customerdeliverylocationaddinfo に行が無い）
--   のため、確定受注であっても帳票から漏れていた。
--   得意先 300 は既存 3,065 件すべてが '1 個別' で、'0 BOX' は 1 件も無い。
--
-- 対象外:
--   得意先 240（ケータリング）は '0 BOX' が 4 件実在するため、未設定 5 件は
--   個別に業務判断が必要。本スクリプトでは触らない。
--
-- 実行後の確認クエリは末尾に記載。
-- ============================================================================

BEGIN;

-- 事前件数（想定: to_update = 5, to_insert = 20）
SELECT
  (SELECT count(*) FROM customerdeliverylocationaddinfo cla
    WHERE ltrim(cla.customercode, '0') = '300'
      AND COALESCE(cla.addinfo08, '') = '')                        AS to_update,
  (SELECT count(*) FROM customerdeliverylocation cdl
    WHERE ltrim(cdl.customercode, '0') = '300'
      AND NOT EXISTS (SELECT 1 FROM customerdeliverylocationaddinfo x
                       WHERE x.customercode = cdl.customercode
                         AND x.deliverylocationcode = cdl.locationcode)) AS to_insert;

-- 1) 追加情報の行はあるが addinfo08 が未設定のもの（5 件想定）
UPDATE customerdeliverylocationaddinfo
   SET addinfo08 = '1 個別'
 WHERE ltrim(customercode, '0') = '300'
   AND COALESCE(addinfo08, '') = '';

-- 2) 追加情報の行そのものが無いもの（20 件想定）
--    addinfoid は customerdeliverylocationaddinfo_addinfoid_seq の既定値に任せる
INSERT INTO customerdeliverylocationaddinfo (customercode, deliverylocationcode, addinfo08)
SELECT cdl.customercode, cdl.locationcode, '1 個別'
  FROM customerdeliverylocation cdl
 WHERE ltrim(cdl.customercode, '0') = '300'
   AND NOT EXISTS (SELECT 1 FROM customerdeliverylocationaddinfo x
                    WHERE x.customercode = cdl.customercode
                      AND x.deliverylocationcode = cdl.locationcode);

-- 事後確認（想定: remaining = 0）
SELECT count(*) AS remaining
  FROM customerdeliverylocation cdl
  LEFT JOIN customerdeliverylocationaddinfo cla
         ON cla.customercode = cdl.customercode
        AND cla.deliverylocationcode = cdl.locationcode
 WHERE ltrim(cdl.customercode, '0') = '300'
   AND COALESCE(cla.addinfo08, '') = '';

COMMIT;

-- ============================================================================
-- 実行後の確認クエリ（得意先ごとの addinfo08 内訳）
-- ============================================================================
-- SELECT ltrim(cdl.customercode,'0') AS cust,
--        CASE WHEN cla.customercode IS NULL THEN 'NO_ROW'
--             WHEN COALESCE(cla.addinfo08,'') = '' THEN 'EMPTY'
--             ELSE cla.addinfo08 END AS addinfo08,
--        count(*) AS locations
--   FROM customerdeliverylocation cdl
--   LEFT JOIN customerdeliverylocationaddinfo cla
--          ON cla.customercode = cdl.customercode
--         AND cla.deliverylocationcode = cdl.locationcode
--  WHERE ltrim(cdl.customercode,'0') IN ('240','300','310')
--  GROUP BY 1,2 ORDER BY 1,2;
