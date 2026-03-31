import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  exportPreparationCsv,
  exportPreparationPdf,
  fetchMajorClassifications,
  fetchMiddleClassifications,
  groupKeyString,
  searchPreparationWork,
} from '../api/preparationClient';
import type { ClassificationOption, PreparationWorkGroup, PreparationWorkGroupKey } from '../types/preparation';

function formatDelvedtDisplay(yyyymmdd: string): string {
  if (yyyymmdd.length !== 8) return yyyymmdd;
  return `${yyyymmdd.slice(0, 4)}-${yyyymmdd.slice(4, 6)}-${yyyymmdd.slice(6, 8)}`;
}

export default function PreparationWorkPage() {
  const [deliveryDate, setDeliveryDate] = useState('');
  const [slot, setSlot] = useState('');
  const [itemcd, setItemcd] = useState('');
  const [majorId, setMajorId] = useState<number | ''>('');
  const [middleId, setMiddleId] = useState<number | ''>('');
  const [majors, setMajors] = useState<ClassificationOption[]>([]);
  const [middles, setMiddles] = useState<ClassificationOption[]>([]);
  const [groups, setGroups] = useState<PreparationWorkGroup[]>([]);
  const [selectedKeys, setSelectedKeys] = useState<Set<string>>(new Set());
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [exporting, setExporting] = useState<'csv' | 'pdf' | null>(null);

  useEffect(() => {
    fetchMajorClassifications()
      .then(setMajors)
      .catch((e) => setError(e instanceof Error ? e.message : '大分類の取得に失敗しました'));
  }, []);

  useEffect(() => {
    setMiddleId('');
    setMiddles([]);
    if (majorId === '') return;
    fetchMiddleClassifications(majorId)
      .then(setMiddles)
      .catch(() => setMiddles([]));
  }, [majorId]);

  const handleSearch = useCallback(async () => {
    if (!deliveryDate.trim()) {
      alert('納期を入力してください');
      return;
    }
    setError(null);
    setLoading(true);
    try {
      const res = await searchPreparationWork({
        deliveryDate,
        slot,
        itemcd,
        majorId,
        middleId,
      });
      setGroups(res.groups ?? []);
      setSelectedKeys(new Set());
    } catch (e) {
      setError(e instanceof Error ? e.message : '検索に失敗しました');
      setGroups([]);
    } finally {
      setLoading(false);
    }
  }, [deliveryDate, slot, itemcd, majorId, middleId]);

  const toggleGroup = useCallback((key: PreparationWorkGroupKey) => {
    const s = groupKeyString(key);
    setSelectedKeys((prev) => {
      const next = new Set(prev);
      if (next.has(s)) next.delete(s);
      else next.add(s);
      return next;
    });
  }, []);

  const selectAll = useCallback(() => {
    setSelectedKeys(new Set(groups.map((g) => groupKeyString(g.key))));
  }, [groups]);

  const deselectAll = useCallback(() => setSelectedKeys(new Set()), []);

  const selectedGroupKeys = useCallback((): PreparationWorkGroupKey[] => {
    return groups.filter((g) => selectedKeys.has(groupKeyString(g.key))).map((g) => g.key);
  }, [groups, selectedKeys]);

  const downloadBlob = (blob: Blob, filename: string) => {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  };

  const handleCsv = useCallback(async () => {
    const keys = selectedGroupKeys();
    if (keys.length === 0) {
      alert('グループを選択してください');
      return;
    }
    setExporting('csv');
    setError(null);
    try {
      const blob = await exportPreparationCsv(
        { deliveryDate, slot, itemcd, majorId, middleId },
        keys
      );
      downloadBlob(blob, '作業前準備書.csv');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'CSV 出力に失敗しました');
    } finally {
      setExporting(null);
    }
  }, [deliveryDate, slot, itemcd, majorId, middleId, selectedGroupKeys]);

  const handlePdf = useCallback(async () => {
    const keys = selectedGroupKeys();
    if (keys.length === 0) {
      alert('グループを選択してください');
      return;
    }
    setExporting('pdf');
    setError(null);
    try {
      const blob = await exportPreparationPdf(
        { deliveryDate, slot, itemcd, majorId, middleId },
        keys
      );
      downloadBlob(blob, '作業前準備書.pdf');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'PDF 出力に失敗しました');
    } finally {
      setExporting(null);
    }
  }, [deliveryDate, slot, itemcd, majorId, middleId, selectedGroupKeys]);

  return (
    <div className="container">
      <header>
        <h1>作業前準備書</h1>
        <p>
          <Link to="/">袋詰指示書・ラベルへ</Link>
        </p>
      </header>

      <section className="search-section">
        <h2>検索条件</h2>
        <div className="search-form">
          <div className="form-group">
            <label htmlFor="pw-delv">納期</label>
            <input
              id="pw-delv"
              type="date"
              value={deliveryDate}
              onChange={(e) => setDeliveryDate(e.target.value)}
            />
          </div>
          <div className="form-group">
            <label htmlFor="pw-slot">便</label>
            <input
              id="pw-slot"
              type="text"
              placeholder="slotcode / 便名の一部"
              value={slot}
              onChange={(e) => setSlot(e.target.value)}
            />
          </div>
          <div className="form-group">
            <label htmlFor="pw-item">品目コード</label>
            <input
              id="pw-item"
              type="text"
              value={itemcd}
              onChange={(e) => setItemcd(e.target.value)}
            />
          </div>
          <div className="form-group">
            <label htmlFor="pw-major">大分類</label>
            <select
              id="pw-major"
              value={majorId === '' ? '' : String(majorId)}
              onChange={(e) => setMajorId(e.target.value === '' ? '' : Number(e.target.value))}
            >
              <option value="">（指定なし）</option>
              {majors.map((m) => (
                <option key={m.id} value={m.id}>
                  {m.name || m.code}
                </option>
              ))}
            </select>
          </div>
          <div className="form-group">
            <label htmlFor="pw-middle">中分類</label>
            <select
              id="pw-middle"
              value={middleId === '' ? '' : String(middleId)}
              onChange={(e) => setMiddleId(e.target.value === '' ? '' : Number(e.target.value))}
              disabled={majorId === ''}
            >
              <option value="">（指定なし）</option>
              {middles.map((m) => (
                <option key={m.id} value={m.id}>
                  {m.name || m.code}
                </option>
              ))}
            </select>
          </div>
          <button type="button" className="btn btn-primary" onClick={handleSearch} disabled={loading}>
            {loading ? '検索中…' : '検索'}
          </button>
        </div>
      </section>

      {error && <p className="error-message">{error}</p>}

      {groups.length > 0 && (
        <section className="results-section">
          <h2>検索結果（{groups.length} グループ）</h2>
          <div className="table-toolbar">
            <button type="button" className="btn btn-secondary" onClick={selectAll}>
              すべて選択
            </button>
            <button type="button" className="btn btn-secondary" onClick={deselectAll}>
              すべて解除
            </button>
            <button
              type="button"
              className="btn btn-primary"
              onClick={handleCsv}
              disabled={exporting !== null}
            >
              {exporting === 'csv' ? '出力中…' : 'データ出力（CSV）'}
            </button>
            <button
              type="button"
              className="btn btn-primary"
              onClick={handlePdf}
              disabled={exporting !== null}
            >
              {exporting === 'pdf' ? '生成中…' : '帳票印刷（PDF）'}
            </button>
          </div>
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th className="col-check" />
                  <th>日付</th>
                  <th>大分類</th>
                  <th>中分類</th>
                  <th className="col-num">件数</th>
                </tr>
              </thead>
              <tbody>
                {groups.map((g) => {
                  const ks = groupKeyString(g.key);
                  return (
                    <tr key={ks}>
                      <td>
                        <input
                          type="checkbox"
                          checked={selectedKeys.has(ks)}
                          onChange={() => toggleGroup(g.key)}
                        />
                      </td>
                      <td>{formatDelvedtDisplay(g.delvedt)}</td>
                      <td>{g.majorClassificationName}</td>
                      <td>{g.middleClassificationName}</td>
                      <td className="col-num">{g.lineCount}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {!loading && deliveryDate && groups.length === 0 && !error && (
        <p className="no-results">該当するグループがありません。検索を実行してください。</p>
      )}
    </div>
  );
}
