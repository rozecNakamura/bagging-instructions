/**
 * API クライアント（契約: prddt, itemcd, jobord_prkeys, print_type）
 */

import type { BaggingInputPayload } from '../types/api';

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

export async function searchBaggingGroups(productionDate: string, productCode: string) {
  const prddt = toPrddt(productionDate);
  const params = new URLSearchParams({ prddt, itemcd: productCode || '' });
  const res = await fetch(`${API_BASE}/search/bagging?${params}`);
  if (!res.ok) throw new Error(`検索エラー: ${res.status}`);
  return res.json();
}

export async function getBaggingInput(prddt: string, itemcd: string, jobordPrkeys?: number[]) {
  const params = new URLSearchParams({ prddt, itemcd });
  for (const pk of jobordPrkeys ?? []) {
    params.append('jobord_prkeys', String(pk));
  }
  const res = await fetch(`${API_BASE}/bagging/input?${params}`);
  if (!res.ok) throw new Error(`取得エラー: ${res.status}`);
  return res.json();
}

export async function saveBaggingInput(
  prddt: string,
  itemcd: string,
  payload: BaggingInputPayload,
  jobordPrkeys?: number[],
) {
  const res = await fetch(`${API_BASE}/bagging/input`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ prddt, itemcd, jobord_prkeys: jobordPrkeys ?? [], payload }),
  });
  if (!res.ok) throw new Error(`登録エラー: ${res.status}`);
  return res.json();
}

export async function importBaggingInput(body: Record<string, unknown>) {
  const res = await fetch(`${API_BASE}/bagging/input/import`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) throw new Error(`取込エラー: ${res.status}`);
  return res.json();
}

export async function fetchBaggingRequiredQuantities(jobordPrkeys: number[]) {
  const res = await fetch(`${API_BASE}/bagging/required-quantities`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ jobord_prkeys: jobordPrkeys }),
  });
  if (!res.ok) throw new Error(`エラー: ${res.status}`);
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

export async function calculateBagging(
  jobordPrkeys: number[],
  printType: 'instruction' | 'label',
  useSavedInput = false
) {
  const res = await fetch(`${API_BASE}/bagging/calculate`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      jobord_prkeys: jobordPrkeys,
      print_type: printType,
      use_saved_input: useSavedInput,
    }),
  });
  if (!res.ok) throw new Error(`計算エラー: ${res.status}`);
  return res.json();
}
