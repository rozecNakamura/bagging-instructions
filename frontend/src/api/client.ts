/**
 * API client (bagging search only; registration/print use static UI — see App.tsx).
 */

const API_BASE = '/api';

function toPrddt(dateStr: string): string {
  if (!dateStr) return '';
  return dateStr.replace(/-/g, '');
}

export async function searchBaggingGroups(productionDate: string, productCode: string) {
  const prddt = toPrddt(productionDate);
  const params = new URLSearchParams({ prddt, itemcd: productCode || '' });
  const res = await fetch(`${API_BASE}/search/bagging?${params}`);
  if (!res.ok) throw new Error(`検索エラー: ${res.status}`);
  return res.json();
}
