import type { FormEvent } from 'react';

interface SearchFormProps {
  productionDate: string;
  productCode: string;
  onProductionDateChange: (v: string) => void;
  onProductCodeChange: (v: string) => void;
  onSearch: () => void;
  loading: boolean;
}

export function SearchForm({
  productionDate,
  productCode,
  onProductionDateChange,
  onProductCodeChange,
  onSearch,
  loading,
}: SearchFormProps) {
  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    onSearch();
  };

  return (
    <section className="search-section">
      <h2>受注明細検索</h2>
      <form className="search-form" onSubmit={handleSubmit}>
        <div className="form-group">
          <label htmlFor="productionDate">製造日:</label>
          <input
            type="date"
            id="productionDate"
            value={productionDate}
            onChange={(e) => onProductionDateChange(e.target.value)}
          />
        </div>
        <div className="form-group">
          <label htmlFor="productCode">品目コード:</label>
          <input
            type="text"
            id="productCode"
            placeholder="品目コードを入力（部分一致）"
            value={productCode}
            onChange={(e) => onProductCodeChange(e.target.value)}
          />
        </div>
        <button type="submit" className="btn btn-primary" disabled={loading}>
          {loading ? '検索中...' : '検索'}
        </button>
      </form>
    </section>
  );
}
