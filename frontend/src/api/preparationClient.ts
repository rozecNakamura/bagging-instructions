import type { ClassificationOption, PreparationWorkGroup, PreparationWorkGroupKey } from '../types/preparation';

const API_BASE = '/api';

function toDelvedt(dateStr: string): string {
  if (!dateStr) return '';
  return dateStr.replace(/-/g, '');
}

export async function fetchMajorClassifications(): Promise<ClassificationOption[]> {
  const res = await fetch(`${API_BASE}/product-label/major-classifications`);
  if (!res.ok) throw new Error(`大分類取得エラー: ${res.status}`);
  return res.json();
}

export async function fetchMiddleClassifications(majorId: number): Promise<ClassificationOption[]> {
  const params = new URLSearchParams({ majorclassificationid: String(majorId) });
  const res = await fetch(`${API_BASE}/preparation-work/middle-classifications?${params}`);
  if (!res.ok) throw new Error(`中分類取得エラー: ${res.status}`);
  return res.json();
}

export async function searchPreparationWork(params: {
  deliveryDate: string;
  slot: string;
  itemcd: string;
  majorId: number | '';
  middleId: number | '';
}): Promise<{ total: number; groups: PreparationWorkGroup[] }> {
  const delvedt = toDelvedt(params.deliveryDate);
  const sp = new URLSearchParams({ delvedt });
  if (params.slot.trim()) sp.set('slot', params.slot.trim());
  if (params.itemcd.trim()) sp.set('itemcd', params.itemcd.trim());
  if (params.majorId !== '') sp.set('majorclassificationid', String(params.majorId));
  if (params.middleId !== '') sp.set('middleclassificationid', String(params.middleId));

  const res = await fetch(`${API_BASE}/preparation-work/search?${sp}`);
  if (!res.ok) throw new Error(`検索エラー: ${res.status}`);
  return res.json();
}

export async function exportPreparationCsv(
  filter: {
    deliveryDate: string;
    slot: string;
    itemcd: string;
    majorId: number | '';
    middleId: number | '';
  },
  groupKeys: PreparationWorkGroupKey[]
): Promise<Blob> {
  const body = {
    delvedt: toDelvedt(filter.deliveryDate),
    slot: filter.slot.trim() || null,
    itemcd: filter.itemcd.trim() || null,
    majorclassificationid: filter.majorId === '' ? null : filter.majorId,
    middleclassificationid: filter.middleId === '' ? null : filter.middleId,
    groupKeys: groupKeys.map((k) => ({
      delvedt: k.delvedt,
      majorClassificationCode: k.majorClassificationCode,
      middleClassificationCode: k.middleClassificationCode,
    })),
  };
  const res = await fetch(`${API_BASE}/preparation-work/csv`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error((err as { detail?: string }).detail || `CSV エラー: ${res.status}`);
  }
  return res.blob();
}

export async function exportPreparationPdf(
  filter: {
    deliveryDate: string;
    slot: string;
    itemcd: string;
    majorId: number | '';
    middleId: number | '';
  },
  groupKeys: PreparationWorkGroupKey[]
): Promise<Blob> {
  const body = {
    delvedt: toDelvedt(filter.deliveryDate),
    slot: filter.slot.trim() || null,
    itemcd: filter.itemcd.trim() || null,
    majorclassificationid: filter.majorId === '' ? null : filter.majorId,
    middleclassificationid: filter.middleId === '' ? null : filter.middleId,
    groupKeys: groupKeys.map((k) => ({
      delvedt: k.delvedt,
      majorClassificationCode: k.majorClassificationCode,
      middleClassificationCode: k.middleClassificationCode,
    })),
  };
  const res = await fetch(`${API_BASE}/preparation-work/pdf`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error((err as { detail?: string }).detail || `PDF エラー: ${res.status}`);
  }
  return res.blob();
}

export function groupKeyString(k: PreparationWorkGroupKey): string {
  return JSON.stringify({
    delvedt: k.delvedt,
    majorClassificationCode: k.majorClassificationCode ?? '',
    middleClassificationCode: k.middleClassificationCode ?? '',
  });
}
