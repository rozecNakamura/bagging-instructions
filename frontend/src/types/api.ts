/** API 契約に合わせた型（snake_case の JSON をそのまま扱う） */

export interface JobordItem {
  prkey: number;
  prddt: string | null;
  delvedt: string | null;
  shptm: string | null;
  cuscd: string | null;
  shpctrcd: string | null;
  itemcd: string | null;
  jobordmernm: string | null;
  jobordqun: number;
}

export interface SearchResponse {
  total: number;
  items: JobordItem[];
}

export interface BaggingInstructionItem {
  shpctrcd: string | null;
  shpctrnm: string;
  itemcd: string;
  itemnm: string;
  delvedt: string;
  shptm: string | null;
  planned_quantity: number;
  adjusted_quantity: number;
  standard_bags: number;
  irregular_quantity: number;
  prddt: string | null;
  current_stock: number;
  seasoning_amounts: SeasoningAmount[];
  jobordno?: string | null;
  jobordmernm?: string | null;
}

export interface SeasoningAmount {
  citemcd: string;
  calculated_amount: number;
}

export interface BaggingInstructionResponse {
  items: BaggingInstructionItem[];
}

export interface LabelItem {
  label_type: string;
  delvedt: string;
  shptm: string | null;
  itemcd: string;
  itemnm: string;
  expiry_date?: string | null;
  count?: number;
  irregular_quantity?: number;
  shpctrnm?: string | null;
}

export interface LabelResponse {
  items: LabelItem[];
}
