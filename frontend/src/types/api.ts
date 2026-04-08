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

export interface BaggingSearchGroup {
  prddt: string;
  itemcd: string;
  itemnm: string | null;
  total_jobordqun: number;
  unit_code: string | null;
  unit_name: string | null;
  line_prkeys: number[];
}

export interface BaggingSearchGroupResponse {
  total: number;
  groups: BaggingSearchGroup[];
}

export interface BaggingInputLine {
  citemcd: string;
  input_order?: number | null;
  spec_qty: number | null;
  total_qty: number | null;
}

export interface BaggingInputPayload {
  lines: BaggingInputLine[];
  /** 親完成品出来高（合計）。登録済み投入量で印刷時に施設別按分に使用 */
  parent_yield_quantity?: number | null;
}

export interface BaggingInputGetResponse {
  prddt: string;
  itemcd: string;
  payload: BaggingInputPayload | null;
  updated_at: string | null;
}

export interface BaggingRequiredQuantitiesResponse {
  total_order_quantity: number;
  lines: BaggingInputLine[];
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
  /** 在庫・出来高用（受注合算） */
  quantity_for_inventory?: number;
  /** 指示書用（切上げ後等） */
  quantity_for_instruction?: number;
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

export interface BaggingIngredientDisplayRow {
  citemcd: string;
  spec_qty: number | null;
  total_qty: number | null;
  unit_name: string | null;
}

export interface BaggingInstructionResponse {
  items: BaggingInstructionItem[];
  ingredient_display_rows?: BaggingIngredientDisplayRow[] | null;
}

export interface LabelItem {
  label_type: string;
  delvedt: string;
  shptm: string | null;
  itemcd: string;
  itemnm: string;
  expiry_date?: string | null;
  strtemp?: string | null;
  /** 殺菌時間（秒） */
  steritime?: number | null;
  kikunip?: number | null;
  standard_fill_qty?: number | null;
  count?: number;
  irregular_quantity?: number;
  shpctrnm?: string | null;
}

export interface LabelResponse {
  items: LabelItem[];
}
