import { useState, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { SearchForm } from './components/SearchForm';
import { ResultTable } from './components/ResultTable';
import { PrintSection } from './components/PrintSection';
import { searchOrders, calculateBagging } from './api/client';
import type { JobordItem, SearchResponse, BaggingInstructionResponse, LabelResponse } from './types/api';

export default function App() {
  const [productionDate, setProductionDate] = useState('');
  const [productCode, setProductCode] = useState('');
  const [items, setItems] = useState<JobordItem[]>([]);
  const [selectedPrkeys, setSelectedPrkeys] = useState<Set<number>>(new Set());
  const [searchLoading, setSearchLoading] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);

  const handleSearch = useCallback(async () => {
    if (!productionDate.trim()) {
      alert('製造日を入力してください');
      return;
    }
    setSearchError(null);
    setSearchLoading(true);
    try {
      const res: SearchResponse = await searchOrders(productionDate, productCode);
      setItems(res.items ?? []);
      setSelectedPrkeys(new Set());
    } catch (e) {
      const msg = e instanceof Error ? e.message : '検索に失敗しました';
      setSearchError(msg);
      setItems([]);
    } finally {
      setSearchLoading(false);
    }
  }, [productionDate, productCode]);

  const togglePrkey = useCallback((prkey: number) => {
    setSelectedPrkeys((prev) => {
      const next = new Set(prev);
      if (next.has(prkey)) next.delete(prkey);
      else next.add(prkey);
      return next;
    });
  }, []);

  const selectAll = useCallback(() => {
    setSelectedPrkeys(new Set(items.map((i) => i.prkey)));
  }, [items]);

  const deselectAll = useCallback(() => {
    setSelectedPrkeys(new Set());
  }, []);

  const handleCalculate = useCallback(
    async (printType: 'instruction' | 'label') => {
      return calculateBagging(Array.from(selectedPrkeys), printType) as Promise<
        BaggingInstructionResponse | LabelResponse
      >;
    },
    [selectedPrkeys]
  );

  return (
    <div className="container">
      <header>
        <h1>袋詰指示書・ラベル管理システム</h1>
        <p>
          <Link to="/preparation-work">作業前準備書</Link>
        </p>
      </header>

      <SearchForm
        productionDate={productionDate}
        productCode={productCode}
        onProductionDateChange={setProductionDate}
        onProductCodeChange={setProductCode}
        onSearch={handleSearch}
        loading={searchLoading}
      />

      {searchError && <p className="error-message">{searchError}</p>}

      {items.length > 0 && (
        <>
          <ResultTable
            items={items}
            selectedPrkeys={selectedPrkeys}
            onToggle={togglePrkey}
            onSelectAll={selectAll}
            onDeselectAll={deselectAll}
          />
          <PrintSection selectedPrkeys={Array.from(selectedPrkeys)} onCalculate={handleCalculate} />
        </>
      )}

      {items.length === 0 && !searchLoading && productionDate && (
        <p className="no-results">該当するデータが見つかりませんでした。</p>
      )}
    </div>
  );
}
