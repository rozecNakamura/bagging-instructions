import { useState, useEffect, useCallback } from 'react';
import type {
  BaggingSearchGroup,
  BaggingInstructionResponse,
  LabelResponse,
  BaggingInputLine,
  BaggingInputPayload,
  BaggingInstructionItem,
} from '../types/api';
import {
  getBaggingInput,
  saveBaggingInput,
  importBaggingInput,
  fetchBaggingRequiredQuantities,
  calculateBagging,
} from '../api/client';

interface LineEdit {
  input_order: number;
  citemcd: string;
  spec_qty: string;
  total_qty: string;
}

function matchSavedLine(
  savedLines: BaggingInputLine[] | undefined,
  reqLine: BaggingInputLine,
  index: number,
): BaggingInputLine | undefined {
  const io = reqLine.input_order != null ? reqLine.input_order : index + 1;
  if (!savedLines?.length) return undefined;
  return savedLines.find((sl) => {
    if (sl.input_order != null) return sl.input_order === io && sl.citemcd === reqLine.citemcd;
    return sl.citemcd === reqLine.citemcd;
  });
}

interface Props {
  productionDate: string;
  group: BaggingSearchGroup | null;
}

function toPrddt(iso: string) {
  return iso.replace(/-/g, '');
}

export function BaggingRegistrationPanel({ productionDate, group }: Props) {
  const [lines, setLines] = useState<LineEdit[]>([]);
  const [printType, setPrintType] = useState<'instruction' | 'label'>('instruction');
  const [useSavedInput, setUseSavedInput] = useState(true);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [preview, setPreview] = useState<BaggingInstructionResponse | LabelResponse | null>(null);
  const [parentYield, setParentYield] = useState('');

  const prddtApi = toPrddt(productionDate || '') || group?.prddt || '';

  const loadLines = useCallback(async () => {
    if (!group || !prddtApi) return;
    setError(null);
    setLoading(true);
    try {
      const required = await fetchBaggingRequiredQuantities(group.line_prkeys);
      let savedPayload = null;
      try {
        const saved = await getBaggingInput(prddtApi, group.itemcd, group.line_prkeys);
        savedPayload = saved.payload;
      } catch {
        savedPayload = null;
      }
      const reqLines = required.lines ?? [];
      setLines(
        reqLines.map((reqLine: BaggingInputLine, j: number) => {
          const io = reqLine.input_order != null ? reqLine.input_order : j + 1;
          const sl = matchSavedLine(savedPayload?.lines, reqLine, j);
          return {
            input_order: io,
            citemcd: reqLine.citemcd || '',
            spec_qty: sl?.spec_qty != null ? String(sl.spec_qty) : '',
            total_qty: sl?.total_qty != null ? String(sl.total_qty) : '0',
          };
        })
      );
      const py = savedPayload?.parent_yield_quantity;
      setParentYield(py != null && Number.isFinite(Number(py)) ? String(py) : '');
    } catch (e) {
      setError(e instanceof Error ? e.message : '読込エラー');
      setLines([]);
      setParentYield('');
    } finally {
      setLoading(false);
    }
  }, [group, prddtApi]);

  useEffect(() => {
    if (group) void loadLines();
    else setLines([]);
  }, [group, loadLines]);

  const setRequiredDefaults = async () => {
    if (!group) return;
    setLoading(true);
    setError(null);
    try {
      const required = await fetchBaggingRequiredQuantities(group.line_prkeys);
      const reqLines = required.lines ?? [];
      setLines(
        reqLines.map((reqLine: BaggingInputLine, j: number) => ({
          input_order: reqLine.input_order != null ? reqLine.input_order : j + 1,
          citemcd: reqLine.citemcd || '',
          spec_qty: '',
          total_qty: reqLine.total_qty != null ? String(reqLine.total_qty) : '',
        }))
      );
    } catch (e) {
      setError(e instanceof Error ? e.message : '必要量セット失敗');
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async () => {
    if (!group || !prddtApi) return;
    setError(null);
    setLoading(true);
    try {
      const pyTrim = parentYield.trim();
      const pyNum = pyTrim === '' ? null : Number(pyTrim);
      const payload: BaggingInputPayload = {
        lines: lines.map((l) => ({
          citemcd: l.citemcd,
          input_order: l.input_order,
          spec_qty: l.spec_qty === '' ? null : Number(l.spec_qty),
          total_qty: l.total_qty === '' ? null : Number(l.total_qty),
        })),
        ...(pyNum != null && Number.isFinite(pyNum) && pyNum > 0
          ? { parent_yield_quantity: pyNum }
          : {}),
      };
      await saveBaggingInput(prddtApi, group.itemcd, payload, group.line_prkeys);
      alert('登録しました。');
    } catch (e) {
      setError(e instanceof Error ? e.message : '登録失敗');
    } finally {
      setLoading(false);
    }
  };

  const handleImport = async () => {
    if (!group || !prddtApi) return;
    const raw = window.prompt(
      'BaggingInputSaveRequestDto 形式の JSON（payload.lines 必須）。製造日・品目・選択行は現在の画面で上書きします。',
    );
    if (raw == null || !String(raw).trim()) return;
    let body: Record<string, unknown>;
    try {
      body = JSON.parse(String(raw).trim()) as Record<string, unknown>;
    } catch (e) {
      setError(e instanceof Error ? e.message : 'JSON 不正');
      return;
    }
    const pl = body.payload as { lines?: unknown[] } | undefined;
    if (!pl?.lines?.length) {
      setError('payload.lines が必要です');
      return;
    }
    setError(null);
    setLoading(true);
    try {
      body.prddt = prddtApi;
      body.itemcd = group.itemcd;
      body.jobord_prkeys = group.line_prkeys;
      await importBaggingInput(body);
      alert('取込みました。');
      await loadLines();
    } catch (e) {
      setError(e instanceof Error ? e.message : '取込失敗');
    } finally {
      setLoading(false);
    }
  };

  const handlePrint = async () => {
    if (!group) return;
    setError(null);
    setLoading(true);
    try {
      const data = await calculateBagging(group.line_prkeys, printType, useSavedInput);
      setPreview(data);
      setTimeout(() => window.print(), 150);
    } catch (e) {
      setError(e instanceof Error ? e.message : '印刷データ取得失敗');
      setPreview(null);
    } finally {
      setLoading(false);
    }
  };

  if (!group) {
    return (
      <p className="no-results" style={{ marginTop: 12 }}>
        検索結果から1件を選択すると、袋詰投入量登録が表示されます。
      </p>
    );
  }

  const u = group.unit_name || group.unit_code || '';

  return (
    <section className="results-section" style={{ marginTop: 16 }}>
      <h2>袋詰投入量登録</h2>
      <p>
        製造日: {group.prddt} 品目: {group.itemcd} {group.itemnm ?? ''} 合計受注: {group.total_jobordqun} {u}
      </p>
      {error != null && <p className="error-message">{error}</p>}
      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>投入順</th>
              <th>子品目コード</th>
              <th>規格数量</th>
              <th>総数量</th>
            </tr>
          </thead>
          <tbody>
            {lines.map((line, i) => (
              <tr key={`${line.citemcd}-${line.input_order}-${i}`}>
                <td className="bagging-reg-cell-readonly">{line.input_order}</td>
                <td className="bagging-reg-cell-readonly">{line.citemcd}</td>
                <td>
                  <input
                    type="number"
                    step="any"
                    value={line.spec_qty}
                    onChange={(e) => {
                      const v = e.target.value;
                      setLines((prev) => prev.map((x, j) => (j === i ? { ...x, spec_qty: v } : x)));
                    }}
                  />
                </td>
                <td>
                  <input
                    type="number"
                    step="any"
                    value={line.total_qty}
                    onChange={(e) => {
                      const v = e.target.value;
                      setLines((prev) => prev.map((x, j) => (j === i ? { ...x, total_qty: v } : x)));
                    }}
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="print-mode" style={{ marginTop: 12 }}>
        <span>印刷タイプ: </span>
        <label>
          <input
            type="radio"
            name="fePrintType"
            checked={printType === 'instruction'}
            onChange={() => setPrintType('instruction')}
          />{' '}
          袋詰指示書
        </label>
        <label style={{ marginLeft: 12 }}>
          <input
            type="radio"
            name="fePrintType"
            checked={printType === 'label'}
            onChange={() => setPrintType('label')}
          />{' '}
          ラベル
        </label>
      </div>
      <p style={{ marginTop: 8 }}>
        <label>
          <input
            type="checkbox"
            checked={useSavedInput}
            onChange={(e) => setUseSavedInput(e.target.checked)}
          />{' '}
          登録済み投入量で印刷（未登録時は受注数ベース）
        </label>
      </p>
      <p style={{ marginTop: 8 }}>
        <label htmlFor="feParentYield">出来高（親・合計）</label>{' '}
        <input
          id="feParentYield"
          type="number"
          step="any"
          style={{ width: '10em', marginLeft: 6 }}
          title="登録済み投入量で印刷時のみ、施設別受注比で按分"
          value={parentYield}
          onChange={(e) => setParentYield(e.target.value)}
        />
      </p>
      <div className="results-header" style={{ marginTop: 12 }}>
        <button type="button" className="btn btn-secondary" onClick={() => void setRequiredDefaults()} disabled={loading}>
          必要量セット
        </button>
        <button type="button" className="btn btn-primary" onClick={() => void handleSave()} disabled={loading}>
          登録
        </button>
        <button type="button" className="btn btn-secondary" onClick={() => void handleImport()} disabled={loading}>
          取込
        </button>
        <button type="button" className="btn btn-success" onClick={() => void handlePrint()} disabled={loading}>
          {loading ? '取得中...' : '印刷'}
        </button>
      </div>
      {preview != null &&
        printType === 'instruction' &&
        'items' in preview &&
        preview.items.length > 0 &&
        'planned_quantity' in preview.items[0] && (
        <div id="print-preview-container" className="print-preview" style={{ marginTop: 16 }}>
          <h3>袋詰指示書プレビュー</h3>
          <table>
            <thead>
              <tr>
                <th>納入場所</th>
                <th>品目</th>
                <th>納品日</th>
                <th>計画数量</th>
                <th>規格袋数</th>
                <th>端数</th>
              </tr>
            </thead>
            <tbody>
              {(preview as BaggingInstructionResponse).items.map((item: BaggingInstructionItem, i: number) => (
                <tr key={i}>
                  <td>{item.shpctrnm}</td>
                  <td>{item.itemnm}</td>
                  <td>{item.delvedt}</td>
                  <td>{item.planned_quantity}</td>
                  <td>{item.standard_bags}</td>
                  <td>{item.irregular_quantity}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
