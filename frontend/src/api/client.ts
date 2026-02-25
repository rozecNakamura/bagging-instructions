/**
 * API クライアント（契約: prddt, itemcd, jobord_prkeys, print_type）
 */

const API_BASE = '/api';

function toPrddt(dateStr: string): string {
  if (!dateStr) return '';
  return dateStr.replace(/-/g, '');
}

export async function searchOrders(productionDate: string, productCode: string) {
  const prddt = toPrddt(productionDate);
  const params = new URLSearchParams({ prddt, itemcd: productCode || '' });
  const res = await fetch(`${API_BASE}/search?${params}`);
  if (!res.ok) throw new Error(`検索エラー: ${res.status}`);
  return res.json();
}

export async function searchOrdersDetail(prkeys: number[]) {
  const res = await fetch(`${API_BASE}/search/detail`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ prkeys }),
  });
  if (!res.ok) throw new Error(`詳細検索エラー: ${res.status}`);
  return res.json();
}

export async function calculateBagging(jobordPrkeys: number[], printType: 'instruction' | 'label') {
  const res = await fetch(`${API_BASE}/bagging/calculate`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ jobord_prkeys: jobordPrkeys, print_type: printType }),
  });
  if (!res.ok) throw new Error(`計算エラー: ${res.status}`);
  return res.json();
}
