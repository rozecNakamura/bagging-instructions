import type { JobordItem } from '../types/api';

interface ResultTableProps {
  items: JobordItem[];
  selectedPrkeys: Set<number>;
  onToggle: (prkey: number) => void;
  onSelectAll: () => void;
  onDeselectAll: () => void;
}

export function ResultTable({
  items,
  selectedPrkeys,
  onToggle,
  onSelectAll,
  onDeselectAll,
}: ResultTableProps) {
  if (items.length === 0) return null;

  const allSelected = items.length > 0 && items.every((i) => selectedPrkeys.has(i.prkey));
  const toggleAll = () => {
    if (allSelected) onDeselectAll();
    else onSelectAll();
  };

  return (
    <section className="results-section">
      <h2>検索結果</h2>
      <div className="results-header">
        <span>{items.length}件</span>
        <div className="checkbox-controls">
          <button type="button" className="btn btn-secondary" onClick={onSelectAll}>
            全選択
          </button>
          <button type="button" className="btn btn-secondary" onClick={onDeselectAll}>
            全解除
          </button>
        </div>
      </div>
      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>
                <input
                  type="checkbox"
                  checked={allSelected}
                  onChange={toggleAll}
                  aria-label="全選択"
                />
              </th>
              <th>製造日</th>
              <th>喫食日</th>
              <th>喫食時間</th>
              <th>得意先コード</th>
              <th>納入場所コード</th>
              <th>品目コード</th>
              <th>受注商品名称</th>
              <th>受注数量</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr
                key={item.prkey}
                onClick={() => onToggle(item.prkey)}
                style={{ cursor: 'pointer' }}
              >
                <td onClick={(e) => e.stopPropagation()}>
                  <input
                    type="checkbox"
                    checked={selectedPrkeys.has(item.prkey)}
                    onChange={() => onToggle(item.prkey)}
                    data-id={item.prkey}
                  />
                </td>
                <td>{item.prddt ?? '-'}</td>
                <td>{item.delvedt ?? '-'}</td>
                <td>{item.shptm ?? '-'}</td>
                <td>{item.cuscd ?? '-'}</td>
                <td>{item.shpctrcd ?? '-'}</td>
                <td>{item.itemcd ?? '-'}</td>
                <td>{item.jobordmernm ?? '-'}</td>
                <td>{item.jobordqun ?? 0}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
