import { useState } from 'react';
import type { BaggingInstructionItem, BaggingInstructionResponse, LabelResponse } from '../types/api';

interface PrintSectionProps {
  selectedPrkeys: number[];
  onCalculate: (printType: 'instruction' | 'label') => Promise<BaggingInstructionResponse | LabelResponse>;
}

export function PrintSection({ selectedPrkeys, onCalculate }: PrintSectionProps) {
  const [printType, setPrintType] = useState<'instruction' | 'label'>('instruction');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [printData, setPrintData] = useState<BaggingInstructionResponse | LabelResponse | null>(null);

  const handlePrint = async () => {
    if (selectedPrkeys.length === 0) {
      alert('印刷する項目を選択してください');
      return;
    }
    setError(null);
    setLoading(true);
    try {
      const data = await onCalculate(printType);
      setPrintData(data);
      setTimeout(() => window.print(), 100);
    } catch (e) {
      const msg = e instanceof Error ? e.message : '印刷データの取得に失敗しました';
      setError(msg);
      setPrintData(null);
    } finally {
      setLoading(false);
    }
  };

  const baggingPreviewItems: BaggingInstructionItem[] =
    printData && printType === 'instruction' && 'items' in printData
      ? (printData as BaggingInstructionResponse).items
      : [];

  return (
    <section className="print-section">
      <h2>印刷設定</h2>
      <div className="print-mode">
        <label>印刷タイプ:</label>
        <div className="radio-group">
          <label>
            <input
              type="radio"
              name="printType"
              value="instruction"
              checked={printType === 'instruction'}
              onChange={() => setPrintType('instruction')}
            />
            袋詰指示書
          </label>
          <label>
            <input
              type="radio"
              name="printType"
              value="label"
              checked={printType === 'label'}
              onChange={() => setPrintType('label')}
            />
            ラベル
          </label>
        </div>
      </div>
      {error != null && <p className="error-message">{error}</p>}
      <button
        type="button"
        className="btn btn-success"
        onClick={handlePrint}
        disabled={loading || selectedPrkeys.length === 0}
      >
        {loading ? '取得中...' : '印刷'}
      </button>
      {baggingPreviewItems.length > 0 && (
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
              {baggingPreviewItems.map((item, i) => (
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
