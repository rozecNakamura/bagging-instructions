-- cstmeat テーブルに prkey カラムを追加

ALTER TABLE cstmeat ADD COLUMN prkey SERIAL;

-- 主キー制約を付ける場合（必要ならコメント解除）
-- ALTER TABLE cstmeat ADD PRIMARY KEY (prkey);
