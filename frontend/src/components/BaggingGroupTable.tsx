import type { BaggingSearchGroup } from '../types/api';

function formatPrddt(prddt: string) {
  if (!prddt || prddt.length !== 8) return prddt || '-';
  return `${prddt.slice(0, 4)}-${prddt.slice(4, 6)}-${prddt.slice(6, 8)}`;
}

interface Props {
  groups: BaggingSearchGroup[];
  selected: BaggingSearchGroup | null;
  onSelect: (g: BaggingSearchGroup) => void;
  onSelectAll: () => void;
  onDeselectAll: () => void;
}

export function BaggingGroupTable({ groups, selected, onSelect, onSelectAll, onDeselectAll }: Props) {
  if (groups.length === 0) return null;

  const isSelected = (g: BaggingSearchGroup) =>
    selected != null && selected.itemcd === g.itemcd && selected.prddt === g.prddt;

  return (
    <section className="results-section">
      <h2>検索結果</h2>
      <div className="results-header">
        <span>{groups.length}件</span>
        <div className="checkbox-controls">
          <button type="button" className="btn btn-secondary" onClick={onSelectAll}>
            先頭を選択
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
              <th>選択</th>
              <th>製造日</th>
              <th>品目コード</th>
              <th>品目名</th>
              <th>受注数量</th>
              <th>単位</th>
            </tr>
          </thead>
          <tbody>
            {groups.map((g) => (
              <tr
                key={`${g.prddt}-${g.itemcd}`}
                onClick={() => onSelect(g)}
                style={{ cursor: 'pointer' }}
              >
                <td onClick={(e) => e.stopPropagation()}>
                  <input
                    type="radio"
                    name="baggingGroup"
                    checked={isSelected(g)}
                    onChange={() => onSelect(g)}
                    aria-label={`選択 ${g.itemcd}`}
                  />
                </td>
                <td>{formatPrddt(g.prddt)}</td>
                <td>{g.itemcd ?? '-'}</td>
                <td>{g.itemnm ?? '-'}</td>
                <td>{g.total_jobordqun ?? 0}</td>
                <td>{g.unit_name || g.unit_code || '-'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
