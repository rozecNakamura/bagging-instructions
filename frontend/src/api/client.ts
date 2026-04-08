/**
 * API client (bagging search only; registration/print use static UI — see App.tsx).
 */

const API_BASE = '/api';

/** YYYYMMDD for API (matches static `normalizePrddt` in api.js). */
export function normalizePrddt(value: string): string {
  if (!value) return '';
  return value.includes('-') ? value.replace(/-/g, '') : value;
}

async function jsonErrorDetailSuffix(res: Response): Promise<string> {
  try {
    const body = (await res.json()) as { detail?: string };
    return body.detail ? ` - ${body.detail}` : '';
  } catch {
    return '';
  }
}

export async function searchBaggingGroups(productionDate: string, productCode: string) {
  const prddt = normalizePrddt(productionDate);
  const params = new URLSearchParams({ prddt, itemcd: productCode || '' });
  const res = await fetch(`${API_BASE}/search/bagging?${params}`);
  if (!res.ok) {
    const detail = await jsonErrorDetailSuffix(res);
    throw new Error(`検索エラー: ${res.status}${detail}`);
  }
  return res.json();
}
