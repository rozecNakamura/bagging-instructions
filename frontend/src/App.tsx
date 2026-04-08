import { useState, useCallback } from 'react';
import { SearchForm } from './components/SearchForm';
import { BaggingGroupTable } from './components/BaggingGroupTable';
import { BaggingRegistrationPanel } from './components/BaggingRegistrationPanel';
import { searchBaggingGroups } from './api/client';
import type { BaggingSearchGroup, BaggingSearchGroupResponse } from './types/api';

export default function App() {
  const [productionDate, setProductionDate] = useState('');
  const [productCode, setProductCode] = useState('');
  const [groups, setGroups] = useState<BaggingSearchGroup[]>([]);
  const [selectedGroup, setSelectedGroup] = useState<BaggingSearchGroup | null>(null);
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
      const res: BaggingSearchGroupResponse = await searchBaggingGroups(productionDate, productCode);
      const list = res.groups ?? [];
      setGroups(list);
      setSelectedGroup(null);
    } catch (e) {
      const msg = e instanceof Error ? e.message : '検索に失敗しました';
      setSearchError(msg);
      setGroups([]);
      setSelectedGroup(null);
    } finally {
      setSearchLoading(false);
    }
  }, [productionDate, productCode]);

  const selectFirst = useCallback(() => {
    if (groups.length > 0) setSelectedGroup(groups[0]);
  }, [groups]);

  const deselectAll = useCallback(() => setSelectedGroup(null), []);

  return (
    <div className="container">
      <header>
        <h1>袋詰指示書・ラベル管理システム</h1>
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

      {groups.length > 0 && (
        <>
          <BaggingGroupTable
            groups={groups}
            selected={selectedGroup}
            onSelect={setSelectedGroup}
            onSelectAll={selectFirst}
            onDeselectAll={deselectAll}
          />
          <BaggingRegistrationPanel productionDate={productionDate} group={selectedGroup} />
        </>
      )}

      {groups.length === 0 && !searchLoading && productionDate && (
        <p className="no-results">該当するデータが見つかりませんでした。</p>
      )}
    </div>
  );
}
