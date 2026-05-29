/**
 * API通信処理
 */

function getApiBaseUrl() {
    if (typeof window !== 'undefined' && typeof window.__API_BASE__ === 'string')
        return window.__API_BASE__;
    return '/api';
}

const API_BASE_URL = getApiBaseUrl();

/** YYYYMMDD for API query params (YYYY-MM-DD → YYYYMMDD; already-compact values pass through). */
export function normalizePrddt(value) {
    if (value == null || value === '') return '';
    const s = String(value);
    return s.includes('-') ? s.replace(/-/g, '') : s;
}

/** @returns {Promise<string>} e.g. ` - server message` or empty */
async function jsonErrorDetailSuffix(response) {
    try {
        const body = await response.json();
        return body.detail ? ` - ${body.detail}` : '';
    } catch {
        return '';
    }
}

/** Bagging JSON APIs: use server `detail` when present. */
async function throwIfBaggingJsonNotOk(response, fallbackLabel) {
    if (response.ok) return;
    const body = await response.json().catch(() => ({}));
    throw new Error(body.detail || `${fallbackLabel}: ${response.status}`);
}

/**
 * 受注明細を検索
 */
export async function searchOrders(productionDate, productCode) {
    try {
        const prddt = normalizePrddt(productionDate);
        const params = new URLSearchParams({
            prddt: prddt,
            itemcd: productCode
        });

        const response = await fetch(`${API_BASE_URL}/search?${params}`);

        if (!response.ok) {
            const detail = await jsonErrorDetailSuffix(response);
            throw new Error(`検索エラー: ${response.status}${detail}`);
        }

        return await response.json();
    } catch (error) {
        console.error('検索APIエラー:', error);
        throw error;
    }
}

/**
 * 袋詰用：製造日・品目で合算した検索グループ
 * @param {string} productionDate
 * @param {string} productCode
 * @param {string} [isComplete] - "" | "true" | "false"
 */
export async function searchBaggingGroups(productionDate, productCode, isComplete) {
    const prddt = normalizePrddt(productionDate);
    const params = new URLSearchParams({ prddt, itemcd: productCode || '' });
    if (isComplete === 'true' || isComplete === 'false') params.set('is_complete', isComplete);
    const response = await fetch(`${API_BASE_URL}/search/bagging?${params}`);
    if (!response.ok) {
        const detail = await jsonErrorDetailSuffix(response);
        throw new Error(`検索エラー: ${response.status}${detail}`);
    }
    return await response.json();
}

/**
 * 袋詰印刷済みとしてマーク
 * @param {string} prddt
 * @param {string} itemcd
 * @param {'instruction'|'label'} printType
 */
export async function markBaggingPrinted(prddt, itemcd, printType) {
    const response = await fetch(`${API_BASE_URL}/bagging/mark-printed`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ prddt, itemcd, print_type: printType })
    });
    await throwIfBaggingJsonNotOk(response, '印刷済み登録エラー');
    return await response.json();
}

/**
 * 作業前準備書貼付け Excel をダウンロード（Blob）
 * @param {string} prddt
 * @param {number[]} jobordPrkeys
 * @returns {Promise<Blob>}
 */
export async function downloadBaggingPreparationExcel(prddt, jobordPrkeys, aggregate = false) {
    const response = await fetch(`${API_BASE_URL}/bagging/preparation-excel`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ prddt: normalizePrddt(prddt), jobord_prkeys: jobordPrkeys || [], aggregate })
    });
    if (!response.ok) {
        const body = await response.json().catch(() => ({}));
        throw new Error(body.detail || `Excelエクスポートエラー: ${response.status}`);
    }
    return await response.blob();
}

/**
 * 袋詰管理 Excel エクスポート URL を構築（検索と同じフィルター条件）
 * @param {string} prddt
 * @param {string} [itemcd]
 * @param {string} [isComplete] - "" | "true" | "false"
 * @returns {string}
 */
export function buildBaggingSearchExportUrl(prddt, itemcd, isComplete) {
    const params = new URLSearchParams({ prddt: normalizePrddt(prddt), itemcd: itemcd || '' });
    if (isComplete === 'true' || isComplete === 'false') params.set('is_complete', isComplete);
    return `${API_BASE_URL}/search/bagging/export?${params}`;
}

/**
 * 袋詰投入量の取得
 */
export async function getBaggingInput(prddt, itemcd, jobordPrkeys) {
    const params = new URLSearchParams({ prddt, itemcd });
    for (const pk of jobordPrkeys || []) {
        params.append('jobord_prkeys', String(pk));
    }
    const response = await fetch(`${API_BASE_URL}/bagging/input?${params}`);
    await throwIfBaggingJsonNotOk(response, '取得エラー');
    return await response.json();
}

/**
 * 袋詰投入量の登録（jobord_prkeys 付きで craftlineaxother.baggedquantity へ保存）
 */
export async function saveBaggingInput(prddt, itemcd, payload, jobordPrkeys) {
    const response = await fetch(`${API_BASE_URL}/bagging/input`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            prddt,
            itemcd,
            jobord_prkeys: jobordPrkeys || [],
            payload
        })
    });
    await throwIfBaggingJsonNotOk(response, '登録エラー');
    return await response.json();
}

/**
 * 必要量セット（BOM 既定）
 */
export async function fetchBaggingRequiredQuantities(jobordPrkeys) {
    const response = await fetch(`${API_BASE_URL}/bagging/required-quantities`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ jobord_prkeys: jobordPrkeys })
    });
    await throwIfBaggingJsonNotOk(response, 'エラー');
    return await response.json();
}

/**
 * 汁仕分表 PDF 生成（rxz テンプレート使用・サーバー側で PDF 生成）
 * @param {Array<{ delvedt: string, shptmDisplay: string, jobordmernm: string, shpctrnm: string, jobordqun: number, addinfo01: string }>} rows
 * @returns {Promise<Blob>}
 */
export async function generateJuicePdfBlob(rows) {
    const response = await fetch(`${API_BASE_URL}/juice/pdf`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            rows: rows.map(r => ({
                delvedt: r.delvedt,
                shptmDisplay: r.shptmDisplay,
                jobordmernm: r.jobordmernm,
                shpctrnm: r.shpctrnm,
                jobordqun: r.jobordqun,
                addinfo01: r.addinfo01,
                addinfo05: r.addinfo05
            }))
        })
    });
    if (!response.ok) {
        const t = await response.text();
        let msg = `PDF生成エラー: ${response.status}`;
        try {
            const j = JSON.parse(t);
            if (j.detail) msg += ' - ' + j.detail;
        } catch (_) { if (t) msg += ' - ' + t; }
        throw new Error(msg);
    }
    return await response.blob();
}

/**
 * ご飯盛り付け指示書 PDF 生成（rxz テンプレート使用・サーバー側で PDF 生成）
 * @param {Array<{ delvedt: string, jobordmernm: string, itemcd: string, cuscd: string, shpctrcd: string, jobordqun: number, quantity: number, addinfo01: string, addinfo08: string, addinfo05: string, shpctrnm: string }>} rows
 * @returns {Promise<Blob>}
 */
export async function generateGohanPdfBlob(rows) {
    const response = await fetch(`${API_BASE_URL}/gohan/pdf`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            rows: rows.map(r => ({
                delvedt: r.delvedt,
                jobordmernm: r.jobordmernm,
                itemcd: r.itemcd ?? '',
                cuscd: r.cuscd ?? '',
                shpctrcd: r.shpctrcd ?? '',
                jobordqun: r.jobordqun,
                quantity: r.quantity ?? 0,
                addinfo01: r.addinfo01 ?? '',
                addinfo08: r.addinfo08 ?? '',
                addinfo05: r.addinfo05 ?? '',
                shpctrnm: r.shpctrnm ?? ''
            }))
        })
    });
    if (!response.ok) {
        const t = await response.text();
        let msg = `PDF生成エラー: ${response.status}`;
        try {
            const j = JSON.parse(t);
            if (j.detail) msg += ' - ' + j.detail;
        } catch (_) { if (t) msg += ' - ' + t; }
        throw new Error(msg);
    }
    return await response.blob();
}

/**
 * 弁当箱盛り付け指示書 PDF 生成（rxz テンプレート使用・サーバー側で PDF 生成）
 * @param {Array} rows
 * @param {string} bentoType okazu | gohan
 * @returns {Promise<Blob>}
 */
export async function generateBentoPdfBlob(rows, bentoType = 'okazu') {
    const response = await fetch(`${API_BASE_URL}/bento/pdf`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            bentoType: bentoType || 'okazu',
            rows: rows.map(r => ({
                delvedt: r.delvedt,
                shptmDisplay: r.shptmDisplay,
                jobordmernm: r.jobordmernm,
                itemcd: r.itemcd ?? '',
                shpctrcd: r.shpctrcd ?? '',
                shpctrnm: r.shpctrnm ?? '',
                jobordqun: r.jobordqun,
                quantity: r.quantity ?? 0,
                addinfo01: r.addinfo01 ?? '',
                addinfo05: r.addinfo05 ?? '',
                info17: r.info17 ?? '',
                foodTypeName: r.foodTypeName ?? ''
            }))
        })
    });
    if (!response.ok) {
        const t = await response.text();
        let msg = `PDF生成エラー: ${response.status}`;
        try {
            const j = JSON.parse(t);
            if (j.detail) msg += ' - ' + j.detail;
        } catch (_) { if (t) msg += ' - ' + t; }
        throw new Error(msg);
    }
    return await response.blob();
}

/**
 * 汁仕分表用：喫食日・品目コードで検索
 */
export async function searchJuice(delvedt, itemcd, mealTime) {
    try {
        let delvedtStr = delvedt;
        if (delvedt && delvedt.includes('-')) {
            delvedtStr = delvedt.replace(/-/g, '');
        }
        const params = new URLSearchParams({
            delvedt: delvedtStr,
            itemcd: itemcd || ''
        });
        if (mealTime) params.set('meal_time', mealTime);
        const response = await fetch(`${API_BASE_URL}/search/juice?${params}`);
        if (!response.ok) {
            let detail = '';
            try {
                const body = await response.json();
                detail = body.detail ? ` - ${body.detail}` : '';
            } catch (_) { /* ignore */ }
            throw new Error(`検索エラー: ${response.status}${detail}`);
        }
        return await response.json();
    } catch (error) {
        console.error('汁仕分表検索APIエラー:', error);
        throw error;
    }
}

/**
 * 納品書用：喫食日で検索（喫食日・納入場所名）
 */
export async function searchDeliveryNote(delvedt, customerType, deliveryRoute) {
    try {
        let delvedtStr = delvedt;
        if (delvedt && delvedt.includes('-')) {
            delvedtStr = delvedt.replace(/-/g, '');
        }
        const params = new URLSearchParams({ delvedt: delvedtStr });
        if (customerType) params.append('customerType', customerType);
        if (deliveryRoute) params.append('deliveryRoute', deliveryRoute);
        const response = await fetch(`${API_BASE_URL}/delivery-note/search?${params}`);
        if (!response.ok) {
            let detail = '';
            try {
                const body = await response.json();
                detail = body.detail ? ` - ${body.detail}` : '';
            } catch (_) { /* ignore */ }
            throw new Error(`検索エラー: ${response.status}${detail}`);
        }
        return await response.json();
    } catch (error) {
        console.error('納品書検索APIエラー:', error);
        throw error;
    }
}

/**
 * 納品書 PDF 生成（納品書.rxz テンプレート・サーバー側で PDF 生成）
 * @param {Array<{ eating_date: string, location_code: string, customer_code: string }>} rows
 * @returns {Promise<Blob>}
 */
export async function generateDeliveryNotePdfBlob(rows) {
    const response = await fetch(`${API_BASE_URL}/delivery-note/pdf`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            rows: rows.map(r => ({
                eating_date: r.eating_date,
                location_code: r.location_code,
                customer_code: r.customer_code ?? ''
            }))
        })
    });
    if (!response.ok) {
        const t = await response.text();
        let msg = `PDF生成エラー: ${response.status}`;
        try {
            const j = JSON.parse(t);
            if (j.detail) msg += ' - ' + j.detail;
        } catch (_) { if (t) msg += ' - ' + t; }
        throw new Error(msg);
    }
    return await response.blob();
}

/**
 * 作業前準備書：中分類マスタ一覧（大分類ID指定）
 */
export async function fetchMiddleClassifications(majorIds) {
    const ids = Array.isArray(majorIds) ? majorIds : [majorIds];
    const params = new URLSearchParams();
    ids.forEach(id => {
        if (id != null && !Number.isNaN(Number(id)) && Number(id) > 0)
            params.append('majorclassificationid', String(id));
    });
    const res = await fetch(`${API_BASE_URL}/preparation-work/middle-classifications?${params}`);
    if (!res.ok) {
        throw new Error(`中分類取得エラー: ${res.status}`);
    }
    return await res.json();
}

/**
 * 作業前準備書：作業区マスタ
 */
export async function fetchPreparationWorkcenters() {
    const res = await fetch(`${API_BASE_URL}/preparation-work/workcenters`);
    if (!res.ok) {
        let detail = '';
        try {
            const body = await res.json();
            detail = body.detail ? ` - ${body.detail}` : '';
        } catch (_) { /* ignore */ }
        throw new Error(`作業区マスタ取得エラー: ${res.status}${detail}`);
    }
    return await res.json();
}

/**
 * 作業前準備書：倉庫マスタ
 */
export async function fetchPreparationWarehouses() {
    const res = await fetch(`${API_BASE_URL}/preparation-work/warehouses`);
    if (!res.ok) {
        let detail = '';
        try {
            const body = await res.json();
            detail = body.detail ? ` - ${body.detail}` : '';
        } catch (_) { /* ignore */ }
        throw new Error(`倉庫マスタ取得エラー: ${res.status}${detail}`);
    }
    return await res.json();
}

/**
 * 作業前準備書：製造便一覧（納期当日の受注付帯より）
 */
export async function fetchPreparationManufacturingRoutes(delvedt) {
    let delvedtStr = delvedt;
    if (delvedt && delvedt.includes('-')) {
        delvedtStr = delvedt.replace(/-/g, '');
    }
    if (!delvedtStr || delvedtStr.length !== 8) {
        return [];
    }
    const params = new URLSearchParams({ delvedt: delvedtStr });
    const res = await fetch(`${API_BASE_URL}/preparation-work/manufacturing-routes?${params}`);
    if (!res.ok) {
        let detail = '';
        try {
            const body = await res.json();
            detail = body.detail ? ` - ${body.detail}` : '';
        } catch (_) { /* ignore */ }
        throw new Error(`製造便一覧取得エラー: ${res.status}${detail}`);
    }
    return await res.json();
}

/**
 * 作業前準備書：検索（納期・製造便・作業区・倉庫・品目・大分類・中分類 → グループ行）
 */
export async function searchPreparationWork(delvedt, options = {}) {
    let delvedtStr = delvedt;
    if (delvedt && delvedt.includes('-')) {
        delvedtStr = delvedt.replace(/-/g, '');
    }
    const params = new URLSearchParams({ delvedt: delvedtStr });
    const itemcd = options.itemcd;
    const majorIds = options.majorIds || [];
    const middleId = options.middleId;
    const manufacturingRouteCodes = options.manufacturingRouteCodes || [];
    const workcenterIds = options.workcenterIds || [];
    const warehouseIds = options.warehouseIds || [];

    (manufacturingRouteCodes || []).forEach(code => {
        const c = code != null ? String(code).trim() : '';
        if (c) params.append('manufacturing_route_code', c);
    });
    (workcenterIds || []).forEach(id => {
        if (id != null && id !== '' && !Number.isNaN(Number(id))) {
            params.append('workcenter_id', String(id));
        }
    });
    (warehouseIds || []).forEach(id => {
        if (id != null && id !== '' && !Number.isNaN(Number(id))) {
            params.append('warehouse_id', String(id));
        }
    });
    if (itemcd && itemcd.trim()) params.set('itemcd', itemcd.trim());
    (majorIds || []).forEach(id => {
        if (id != null && !Number.isNaN(Number(id)) && Number(id) > 0) {
            params.append('majorclassificationid', String(id));
        }
    });
    if (middleId) params.set('middleclassificationid', String(middleId));

    const res = await fetch(`${API_BASE_URL}/preparation-work/search?${params}`);
    if (!res.ok) {
        let detail = '';
        try {
            const body = await res.json();
            detail = body.detail ? ` - ${body.detail}` : '';
        } catch (_) { /* ignore */ }
        throw new Error(`検索エラー: ${res.status}${detail}`);
    }
    return await res.json();
}

/**
 * 作業前準備書：CSV 出力
 */
export async function exportPreparationCsv(filter, groupKeys) {
    const body = {
        delvedt: filter.delvedt,
        manufacturing_route_codes:
            filter.manufacturingRouteCodes && filter.manufacturingRouteCodes.length
                ? filter.manufacturingRouteCodes
                : null,
        workcenter_ids:
            filter.workcenterIds && filter.workcenterIds.length ? filter.workcenterIds : null,
        warehouse_ids:
            filter.warehouseIds && filter.warehouseIds.length ? filter.warehouseIds : null,
        itemcd: filter.itemcd || null,
        majorclassificationid: filter.majorId || null,
        middleclassificationid: filter.middleId || null,
        groupKeys: (groupKeys || []).map(k => ({
            delvedt: k.delvedt,
            majorClassificationCode: k.majorClassificationCode ?? null,
            middleClassificationCode: k.middleClassificationCode ?? null
        }))
    };
    const res = await fetch(`${API_BASE_URL}/preparation-work/csv`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body)
    });
    if (!res.ok) {
        const t = await res.text();
        let msg = `CSV出力エラー: ${res.status}`;
        try {
            const j = JSON.parse(t);
            if (j.detail) msg += ' - ' + j.detail;
        } catch (_) { if (t) msg += ' - ' + t; }
        throw new Error(msg);
    }
    return await res.blob();
}

/**
 * 作業前準備書：PDF 出力
 */
export async function exportPreparationPdf(filter, groupKeys) {
    const body = {
        delvedt: filter.delvedt,
        manufacturing_route_codes:
            filter.manufacturingRouteCodes && filter.manufacturingRouteCodes.length
                ? filter.manufacturingRouteCodes
                : null,
        workcenter_ids:
            filter.workcenterIds && filter.workcenterIds.length ? filter.workcenterIds : null,
        warehouse_ids:
            filter.warehouseIds && filter.warehouseIds.length ? filter.warehouseIds : null,
        itemcd: filter.itemcd || null,
        majorclassificationid: filter.majorId || null,
        middleclassificationid: filter.middleId || null,
        groupKeys: (groupKeys || []).map(k => ({
            delvedt: k.delvedt,
            majorClassificationCode: k.majorClassificationCode ?? null,
            middleClassificationCode: k.middleClassificationCode ?? null
        }))
    };
    const res = await fetch(`${API_BASE_URL}/preparation-work/pdf`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body)
    });
    if (!res.ok) {
        const t = await res.text();
        let msg = `PDF出力エラー: ${res.status}`;
        try {
            const j = JSON.parse(t);
            if (j.detail) msg += ' - ' + j.detail;
        } catch (_) { if (t) msg += ' - ' + t; }
        throw new Error(msg);
    }
    return await res.blob();
}

/**
 * 弁当箱盛り付け指示書用：喫食日・品目コードで検索。bentoType=okazu|gohan。
 */
export async function searchBento(delvedt, itemcd, bentoType) {
    try {
        let delvedtStr = delvedt;
        if (delvedt && delvedt.includes('-')) {
            delvedtStr = delvedt.replace(/-/g, '');
        }
        const params = new URLSearchParams({
            delvedt: delvedtStr,
            itemcd: itemcd || '',
            bento_type: bentoType || 'okazu'
        });
        const response = await fetch(`${API_BASE_URL}/search/bento?${params}`);
        if (!response.ok) {
            let detail = '';
            try {
                const body = await response.json();
                detail = body.detail ? ` - ${body.detail}` : '';
            } catch (_) { /* ignore */ }
            throw new Error(`検索エラー: ${response.status}${detail}`);
        }
        return await response.json();
    } catch (error) {
        console.error('弁当箱盛り付け指示書検索APIエラー:', error);
        throw error;
    }
}

/**
 * ご飯盛り付け指示書用：喫食日・品目コードで検索。
 */
export async function searchGohan(delvedt, itemcd, addinfo08Type) {
    try {
        let delvedtStr = delvedt;
        if (delvedt && delvedt.includes('-')) {
            delvedtStr = delvedt.replace(/-/g, '');
        }
        const params = new URLSearchParams({
            delvedt: delvedtStr,
            itemcd: itemcd || ''
        });
        if (addinfo08Type) params.set('addinfo08_type', addinfo08Type);
        const response = await fetch(`${API_BASE_URL}/search/gohan?${params}`);
        if (!response.ok) {
            let detail = '';
            try {
                const body = await response.json();
                detail = body.detail ? ` - ${body.detail}` : '';
            } catch (_) { /* ignore */ }
            throw new Error(`検索エラー: ${response.status}${detail}`);
        }
        return await response.json();
    } catch (error) {
        console.error('ご飯盛り付け指示書検索APIエラー:', error);
        throw error;
    }
}

/**
 * 個人配送指示書用：喫食日で検索（cstmeat 基準・喫食日・喫食時間・コース）
 */
export async function searchPersonalDelivery(delvedt, variant = 'detail') {
    try {
        let delvedtStr = delvedt;
        if (delvedt && delvedt.includes('-')) {
            delvedtStr = delvedt.replace(/-/g, '');
        }
        const params = new URLSearchParams({ delvedt: delvedtStr, variant: variant || 'detail' });
        const response = await fetch(`${API_BASE_URL}/personal-delivery/search?${params}`);
        if (!response.ok) {
            let detail = '';
            try {
                const body = await response.json();
                detail = body.detail ? ` - ${body.detail}` : '';
            } catch (_) { /* ignore */ }
            throw new Error(`検索エラー: ${response.status}${detail}`);
        }
        return await response.json();
    } catch (error) {
        console.error('個人配送指示書検索APIエラー:', error);
        throw error;
    }
}

/**
 * 個人配送指示書 PDF 生成
 * @param {Array<{ eating_date: string, meal_time: string, course: string }>} rows
 * @param {'detail'|'summary'} variant - 'detail'=個人配送指示書.rxz のみ / 'summary'=個人配送指示書（集計）.rxz のみ
 * @returns {Promise<Blob>}
 */
export async function generatePersonalDeliveryPdfBlob(rows, variant) {
    const response = await fetch(`${API_BASE_URL}/personal-delivery/pdf`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            rows: rows.map(r => ({
                eating_date: r.eating_date,
                meal_time: r.meal_time ?? '',
                course: r.course ?? ''
            })),
            variant: variant
        })
    });
    if (!response.ok) {
        const t = await response.text();
        let msg = `PDF生成エラー: ${response.status}`;
        try {
            const j = JSON.parse(t);
            if (j.detail) msg += ' - ' + j.detail;
        } catch (_) { if (t) msg += ' - ' + t; }
        throw new Error(msg);
    }
    return await response.blob();
}

export async function searchAggregateSummary(fromDate, toDate, itemCode, majorClass, middleClass) {
    const params = new URLSearchParams();
    if (fromDate) params.set('from_date', fromDate);
    if (toDate) params.set('to_date', toDate);
    if (itemCode && itemCode.trim()) params.set('item_code', itemCode.trim());
    // majorClass / middleClass は文字列または配列を許容
    const majors = Array.isArray(majorClass) ? majorClass : (majorClass ? [majorClass] : []);
    const middles = Array.isArray(middleClass) ? middleClass : (middleClass ? [middleClass] : []);
    majors
        .map(s => String(s).trim())
        .filter(s => s)
        .forEach(code => params.append('major_class', code));
    middles
        .map(s => String(s).trim())
        .filter(s => s)
        .forEach(code => params.append('middle_class', code));

    const res = await fetch(`${API_BASE_URL}/aggregate-summary?${params}`);
    if (!res.ok) {
        let detail = '';
        try {
            const body = await res.json();
            detail = body.detail ? ` - ${body.detail}` : '';
        } catch (_) { /* ignore */ }
        throw new Error(`検索エラー: ${res.status}${detail}`);
    }
    return await res.json();
}

export async function exportAggregateSummaryPdf(filter, summaryKeys) {
    const body = {
        filter: {
            from_date: (filter.fromDate && filter.fromDate.includes('-'))
                ? filter.fromDate.replace(/-/g, '')
                : (filter.fromDate || null),
            to_date: (filter.toDate && filter.toDate.includes('-'))
                ? filter.toDate.replace(/-/g, '')
                : (filter.toDate || null),
            item_code: filter.itemCode || null,
            // majorClass / middleClass は配列または単一値を許容
            major_class: null,
            middle_class: null,
            major_classes: Array.isArray(filter.majorClass)
                ? filter.majorClass
                : (filter.majorClass ? [filter.majorClass] : []),
            middle_classes: Array.isArray(filter.middleClass)
                ? filter.middleClass
                : (filter.middleClass ? [filter.middleClass] : [])
        },
        summaryKeys: summaryKeys || []
    };

    const res = await fetch(`${API_BASE_URL}/aggregate-summary/report`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body)
    });
    if (!res.ok) {
        const t = await res.text();
        let msg = `PDF出力エラー: ${res.status}`;
        try {
            const j = JSON.parse(t);
            if (j.detail) msg += ' - ' + j.detail;
        } catch (_) { if (t) msg += ' - ' + t; }
        throw new Error(msg);
    }
    return await res.blob();
}

/**
 * 受注明細詳細を取得（全リレーションデータ含む）
 * 印刷処理の前に呼び出す
 */
/**
 * 現品票：大分類一覧
 * @returns {Promise<{ id: number, code: string, name: string }[]>}
 */
export async function fetchMajorClassifications() {
    const response = await fetch(`${API_BASE_URL}/product-label/major-classifications`);
    if (!response.ok) {
        let detail = '';
        try {
            const body = await response.json();
            detail = body.detail ? ` - ${body.detail}` : '';
        } catch (_) { /* ignore */ }
        throw new Error(`取得エラー: ${response.status}${detail}`);
    }
    return await response.json();
}

/**
 * 集計表用：中分類マスタ一覧（大分類コード指定）
 * @param {string} majorCode
 * @returns {Promise<{ code: string, name: string }[]>}
 */
export async function fetchAggregateMiddleClassifications(majorCode) {
    const params = new URLSearchParams();
    if (majorCode && majorCode.trim()) {
        params.set('major_code', majorCode.trim());
    }
    const response = await fetch(`${API_BASE_URL}/aggregate-summary/middle-classifications?${params.toString()}`);
    if (!response.ok) {
        let detail = '';
        try {
            const body = await response.json();
            detail = body.detail ? ` - ${body.detail}` : '';
        } catch (_) { /* ignore */ }
        throw new Error(`取得エラー: ${response.status}${detail}`);
    }
    return await response.json();
}

export async function fetchProductLabelWorkcenters() {
    const response = await fetch(`${API_BASE_URL}/product-label/workcenters`);
    if (!response.ok) throw new Error(`作業区取得エラー: ${response.status}`);
    return await response.json();
}

export async function fetchProductLabelWarehouses() {
    const response = await fetch(`${API_BASE_URL}/product-label/warehouses`);
    if (!response.ok) throw new Error(`倉庫取得エラー: ${response.status}`);
    return await response.json();
}

/**
 * 現品票検索（MO品目かつBOM最上位品目のみ）
 * @param {object} params
 * @param {string} params.needDate - YYYY-MM-DD または YYYYMMDD（必須）
 * @param {number|string|null} [params.majorClassificationId]
 * @param {string|null} [params.itemCode]
 * @param {number|null} [params.workcenterId]
 * @param {number|null} [params.warehouseId]
 */
export async function searchProductLabel({ needDate, majorClassificationId, itemCode, workcenterId, warehouseId } = {}) {
    let needdateStr = needDate;
    if (needDate && needDate.includes('-')) needdateStr = needDate.replace(/-/g, '');
    const p = new URLSearchParams({ needdate: needdateStr });
    if (majorClassificationId != null && String(majorClassificationId).trim() !== '')
        p.set('majorclassificationid', String(majorClassificationId));
    if (itemCode && itemCode.trim() !== '')
        p.set('itemcode', itemCode.trim());
    if (workcenterId != null && String(workcenterId).trim() !== '')
        p.set('workcenterid', String(workcenterId));
    if (warehouseId != null && String(warehouseId).trim() !== '')
        p.set('warehouseid', String(warehouseId));

    const response = await fetch(`${API_BASE_URL}/product-label/search?${p}`);
    if (!response.ok) {
        let detail = '';
        try { const body = await response.json(); detail = body.detail ? ` - ${body.detail}` : ''; } catch (_) { /* ignore */ }
        throw new Error(`検索エラー: ${response.status}${detail}`);
    }
    return await response.json();
}

/**
 * 現品票 PDF 生成
 * @param {number[]} orderTableIds
 * @param {number} [labelCount=1] - 1ラベルあたりの印刷枚数
 * @param {string} [cutMode="no_cut"] - "cut_on_item_change" | "no_cut"
 * @param {string} [instructionType] - "cut" | "seasoning" | "cooking"（BOM再帰探索の抽出条件）
 * @param {Object|null} [perRowCounts=null] - ordertableid文字列→枚数のマップ
 * @returns {Promise<Blob>}
 */
export async function generateProductLabelPdfBlob(orderTableIds, labelCount = 1, cutMode = 'no_cut', instructionType = '', perRowCounts = null) {
    const bodyObj = { order_table_ids: orderTableIds, label_count: labelCount, cut_mode: cutMode, instruction_type: instructionType || undefined };
    if (perRowCounts && Object.keys(perRowCounts).length > 0) bodyObj.per_row_counts = perRowCounts;
    const response = await fetch(`${API_BASE_URL}/product-label/pdf`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(bodyObj)
    });
    if (!response.ok) {
        const t = await response.text();
        let msg = `PDF生成エラー: ${response.status}`;
        try { const j = JSON.parse(t); if (j.detail) msg += ' - ' + j.detail; } catch (_) { if (t) msg += ' - ' + t; }
        throw new Error(msg);
    }
    return await response.blob();
}

/**
 * 袋詰現品票 PDF — LabelItemDto[] から直接生成。ordertable 不要。
 * @param {Object[]} items - LabelItemDto の配列
 * @returns {Promise<Blob>}
 */
export async function generateBaggingLabelPdfBlob(items) {
    const response = await fetch(`${API_BASE_URL}/bagging/label-pdf`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ items })
    });
    if (!response.ok) {
        const t = await response.text();
        let msg = `PDF生成エラー: ${response.status}`;
        try { const j = JSON.parse(t); if (j.detail) msg += ' - ' + j.detail; } catch (_) { if (t) msg += ' - ' + t; }
        throw new Error(msg);
    }
    return await response.blob();
}

/**
 * 現品票 PDF — 受注明細 ID（salesorderlineid）から ordertable を解決して生成。
 * @param {number[]} salesOrderLineIds
 * @param {number} [labelCount=1]
 * @param {string} [cutMode="no_cut"]
 * @returns {Promise<Blob>}
 */
export async function generateProductLabelPdfBlobFromSalesOrderLines(salesOrderLineIds, labelCount = 1, cutMode = 'no_cut') {
    const response = await fetch(`${API_BASE_URL}/product-label/pdf-from-sales-order-lines`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ sales_order_line_ids: salesOrderLineIds, label_count: labelCount, cut_mode: cutMode })
    });
    if (!response.ok) {
        const t = await response.text();
        let msg = `PDF生成エラー: ${response.status}`;
        try { const j = JSON.parse(t); if (j.detail) msg += ' - ' + j.detail; } catch (_) { if (t) msg += ' - ' + t; }
        throw new Error(msg);
    }
    return await response.blob();
}

export async function searchOrdersDetail(prkeys) {
    try {
        const response = await fetch(`${API_BASE_URL}/search/detail`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({ prkeys }),
        });
        
        if (!response.ok) {
            throw new Error(`詳細検索エラー: ${response.status}`);
        }
        
        return await response.json();
    } catch (error) {
        console.error('詳細検索APIエラー:', error);
        throw error;
    }
}

/**
 * 袋詰指示書・ラベルデータを計算
 */
export async function calculateBagging(jobordPrkeys, printType, useSavedInput = false, expiryDateOverride = null) {
    try {
        const body = {
            jobord_prkeys: jobordPrkeys,
            print_type: printType,
            use_saved_input: useSavedInput
        };
        if (expiryDateOverride) body.expiry_date_override = expiryDateOverride;
        const response = await fetch(`${API_BASE_URL}/bagging/calculate`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(body)
        });
        
        if (!response.ok) {
            const detail = await jsonErrorDetailSuffix(response);
            throw new Error(`計算エラー: ${response.status}${detail}`);
        }

        return await response.json();
    } catch (error) {
        console.error('計算APIエラー:', error);
        throw error;
    }
}

/**
 * 調理指示書：作業名マスタ（classfication3）
 * @returns {Promise<{ code: string, name: string }[]>}
 */
export async function fetchCookingClassification3s() {
    const res = await fetch(`${API_BASE_URL}/cooking-instruction/classification3`);
    if (!res.ok) {
        let detail = '';
        try {
            const body = await res.json();
            detail = body.detail ? ` - ${body.detail}` : '';
        } catch (_) { /* ignore */ }
        throw new Error(`作業名マスタ取得エラー: ${res.status}${detail}`);
    }
    return await res.json();
}

/**
 * 調理指示書：作業区マスタ
 * @returns {Promise<{ id: number, name: string }[]>}
 */
export async function fetchCookingWorkcenters() {
    const res = await fetch(`${API_BASE_URL}/cooking-instruction/workcenters`);
    if (!res.ok) {
        let detail = '';
        try {
            const body = await res.json();
            detail = body.detail ? ` - ${body.detail}` : '';
        } catch (_) { /* ignore */ }
        throw new Error(`作業区マスタ取得エラー: ${res.status}${detail}`);
    }
    return await res.json();
}

/**
 * 調理指示書：便一覧（納期当日の受注より）
 * @param {string} needDate
 * @returns {Promise<{ code: string, name: string }[]>}
 */
export async function fetchCookingSlots(needDate) {
    let needdateStr = needDate;
    if (needDate && needDate.includes('-')) {
        needdateStr = needDate.replace(/-/g, '');
    }
    if (!needdateStr || needdateStr.length !== 8) return [];
    const params = new URLSearchParams({ needdate: needdateStr });
    const res = await fetch(`${API_BASE_URL}/cooking-instruction/slots?${params}`);
    if (!res.ok) {
        let detail = '';
        try {
            const body = await res.json();
            detail = body.detail ? ` - ${body.detail}` : '';
        } catch (_) { /* ignore */ }
        throw new Error(`便一覧取得エラー: ${res.status}${detail}`);
    }
    return await res.json();
}

/**
 * 調理指示書：検索（納期・作業区 ID 複数・便コード複数・作業名コード複数）
 * @param {string} needDate
 * @param {number[]} workcenterIds
 * @param {string[]} slotCodes
 * @param {string[]} [classification3Codes]
 */
export async function searchCookingInstruction(needDate, workcenterIds, slotCodes, classification3Codes) {
    let needdateStr = needDate;
    if (needDate && needDate.includes('-')) {
        needdateStr = needDate.replace(/-/g, '');
    }
    const params = new URLSearchParams({ needdate: needdateStr });
    (workcenterIds || []).forEach(id => {
        if (id != null && id !== '' && !Number.isNaN(Number(id))) {
            params.append('workcenter_id', String(id));
        }
    });
    (slotCodes || []).forEach(code => {
        const c = code != null ? String(code).trim() : '';
        if (c) params.append('slot_code', c);
    });
    (classification3Codes || []).forEach(code => {
        const c = code != null ? String(code).trim() : '';
        if (c) params.append('classification3_code', c);
    });

    const res = await fetch(`${API_BASE_URL}/cooking-instruction/search?${params}`);
    if (!res.ok) {
        let detail = '';
        try {
            const body = await res.json();
            detail = body.detail ? ` - ${body.detail}` : '';
        } catch (_) { /* ignore */ }
        throw new Error(`検索エラー: ${res.status}${detail}`);
    }
    return await res.json();
}

/**
 * 調理指示書：Excel 出力（検索結果をそのまま xlsx 化）
 * @param {string} needDate
 * @param {number[]} workcenterIds
 * @param {string[]} slotCodes
 * @param {string[]} [classification3Codes]
 * @returns {Promise<Blob>}
 */
export async function exportCookingInstructionExcel(needDate, workcenterIds, slotCodes, classification3Codes) {
    let needdateStr = needDate;
    if (needDate && needDate.includes('-')) {
        needdateStr = needDate.replace(/-/g, '');
    }
    const params = new URLSearchParams({ needdate: needdateStr });
    (workcenterIds || []).forEach(id => {
        if (id != null && id !== '' && !Number.isNaN(Number(id))) {
            params.append('workcenter_id', String(id));
        }
    });
    (slotCodes || []).forEach(code => {
        const c = code != null ? String(code).trim() : '';
        if (c) params.append('slot_code', c);
    });
    (classification3Codes || []).forEach(code => {
        const c = code != null ? String(code).trim() : '';
        if (c) params.append('classification3_code', c);
    });

    const res = await fetch(`${API_BASE_URL}/cooking-instruction/export-excel?${params}`);
    if (!res.ok) {
        const t = await res.text();
        let msg = `Excel出力エラー: ${res.status}`;
        try {
            const j = JSON.parse(t);
            if (j.detail) msg += ' - ' + j.detail;
        } catch (_) { if (t) msg += ' - ' + t; }
        throw new Error(msg);
    }
    return await res.blob();
}

/**
 * 調理指示書：PDF 出力
 */
export async function exportCookingInstructionPdf(filter, orderTableIds) {
    const body = {
        needdate: (filter.needDate && filter.needDate.includes('-'))
            ? filter.needDate.replace(/-/g, '')
            : (filter.needDate || null),
        orderTableIds: orderTableIds || []
    };

    const res = await fetch(`${API_BASE_URL}/cooking-instruction/report`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body)
    });
    if (!res.ok) {
        const t = await res.text();
        let msg = `PDF出力エラー: ${res.status}`;
        try {
            const j = JSON.parse(t);
            if (j.detail) msg += ' - ' + j.detail;
        } catch (_) { if (t) msg += ' - ' + t; }
        throw new Error(msg);
    }
    return await res.blob();
}

/**
 * 調味液配合表仕様：作業区マスタ
 * @returns {Promise<{ id: number, name: string }[]>}
 */
export async function fetchProductionInstructionWorkcenters() {
    const res = await fetch(`${API_BASE_URL}/production-instruction/workcenters`);
    if (!res.ok) {
        let detail = '';
        try {
            const body = await res.json();
            detail = body.detail ? ` - ${body.detail}` : '';
        } catch (_) { /* ignore */ }
        throw new Error(`取得エラー: ${res.status}${detail}`);
    }
    return await res.json();
}

/**
 * 調味液配合表仕様：便マスタ
 * @returns {Promise<{ code: string, name: string }[]>}
 */
/**
 * 仕分け照会：便マスタ一覧
 */
export async function fetchSortingInquirySlots() {
    const res = await fetch(`${API_BASE_URL}/sorting-inquiry/slots`);
    if (!res.ok) {
        let detail = '';
        try {
            const body = await res.json();
            detail = body.detail ? ` - ${body.detail}` : '';
        } catch (_) { /* ignore */ }
        throw new Error(`取得エラー: ${res.status}${detail}`);
    }
    return await res.json();
}

/**
 * 仕分け照会：検索（喫食日・便コード複数）
 * @param {string} delvedt
 * @param {string[]} slotCodes
 */
export async function searchSortingInquiry(delvedt, slotCodes, mealTime) {
    let delvedtStr = delvedt;
    if (delvedt && delvedt.includes('-')) {
        delvedtStr = delvedt.replace(/-/g, '');
    }
    const params = new URLSearchParams({ delvedt: delvedtStr });
    const codes = Array.isArray(slotCodes) ? slotCodes : [];
    codes.forEach(c => {
        if (c && String(c).trim()) params.append('slot_code', String(c).trim());
    });
    const mt = mealTime != null ? String(mealTime).trim() : '';
    if (mt) params.set('meal_time', mt);
    const res = await fetch(`${API_BASE_URL}/sorting-inquiry/search?${params}`);
    if (!res.ok) {
        let detail = '';
        try {
            const body = await res.json();
            detail = body.detail ? ` - ${body.detail}` : '';
        } catch (_) { /* ignore */ }
        throw new Error(`検索エラー: ${res.status}${detail}`);
    }
    return await res.json();
}

/**
 * 仕分け照会 Excel（仕分け照会様式）
 */
export async function exportSortingInquiryShiwakeBlob(delvedt, slotCodes, mealTime) {
    let delvedtStr = delvedt;
    if (delvedt && delvedt.includes('-')) {
        delvedtStr = delvedt.replace(/-/g, '');
    }
    const params = new URLSearchParams({ delvedt: delvedtStr });
    (Array.isArray(slotCodes) ? slotCodes : []).forEach(c => {
        if (c && String(c).trim()) params.append('slot_code', String(c).trim());
    });
    const mt = mealTime != null ? String(mealTime).trim() : '';
    if (mt) params.set('meal_time', mt);
    const res = await fetch(`${API_BASE_URL}/sorting-inquiry/export/shiwake-inquiry?${params}`);
    if (!res.ok) {
        const t = await res.text();
        let msg = `Excel出力エラー: ${res.status}`;
        try {
            const j = JSON.parse(t);
            if (j.detail) msg += ' - ' + j.detail;
        } catch (_) { if (t) msg += ' - ' + t; }
        throw new Error(msg);
    }
    return await res.blob();
}

/**
 * 仕分け照会 Excel（仕訳表自動調整様式）
 */
export async function exportSortingInquiryJournalBlob(delvedt, slotCodes, mealTime) {
    let delvedtStr = delvedt;
    if (delvedt && delvedt.includes('-')) {
        delvedtStr = delvedt.replace(/-/g, '');
    }
    const params = new URLSearchParams({ delvedt: delvedtStr });
    (Array.isArray(slotCodes) ? slotCodes : []).forEach(c => {
        if (c && String(c).trim()) params.append('slot_code', String(c).trim());
    });
    const mt = mealTime != null ? String(mealTime).trim() : '';
    if (mt) params.set('meal_time', mt);
    const res = await fetch(`${API_BASE_URL}/sorting-inquiry/export/journal-adjustment?${params}`);
    if (!res.ok) {
        const t = await res.text();
        let msg = `Excel出力エラー: ${res.status}`;
        try {
            const j = JSON.parse(t);
            if (j.detail) msg += ' - ' + j.detail;
        } catch (_) { if (t) msg += ' - ' + t; }
        throw new Error(msg);
    }
    return await res.blob();
}

export async function fetchProductionInstructionSlots(needdate) {
    const nd = needdate ? String(needdate).replace(/-/g, '') : '';
    const params = nd ? `?needdate=${encodeURIComponent(nd)}` : '';
    const res = await fetch(`${API_BASE_URL}/production-instruction/slots${params}`);
    if (!res.ok) {
        let detail = '';
        try {
            const body = await res.json();
            detail = body.detail ? ` - ${body.detail}` : '';
        } catch (_) { /* ignore */ }
        throw new Error(`取得エラー: ${res.status}${detail}`);
    }
    return await res.json();
}

/**
 * 調味液配合表仕様：検索（納期・作業区 ID 複数・便コード複数）
 * @param {string} needDate
 * @param {number[]|number|null|undefined} workcenterIds
 * @param {string[]|string|null|undefined} slotCodes
 */
/**
 * @param {string} needDate
 * @param {number[]|null|undefined} workcenterIds
 * @param {string[]|null|undefined} slotCodes
 * @param {string|null|undefined} itemQuery 品目コード・品目名の部分一致（省略可）
 */
export async function searchProductionInstruction(needDate, workcenterIds, slotCodes, itemQuery) {
    let needdateStr = needDate;
    if (needDate && needDate.includes('-')) {
        needdateStr = needDate.replace(/-/g, '');
    }
    const params = new URLSearchParams({ needdate: needdateStr });
    const wcs = Array.isArray(workcenterIds) ? workcenterIds : (workcenterIds != null ? [workcenterIds] : []);
    const slots = Array.isArray(slotCodes) ? slotCodes : (slotCodes ? [slotCodes] : []);
    wcs
        .map(id => Number(id))
        .filter(id => Number.isFinite(id) && id > 0)
        .forEach(id => params.append('workcenter_id', String(id)));
    slots
        .map(s => String(s).trim())
        .filter(s => s)
        .forEach(code => params.append('slot_code', code));
    const item = itemQuery != null ? String(itemQuery).trim() : '';
    if (item) params.set('item', item);

    const res = await fetch(`${API_BASE_URL}/production-instruction/search?${params}`);
    if (!res.ok) {
        let detail = '';
        try {
            const body = await res.json();
            detail = body.detail ? ` - ${body.detail}` : '';
        } catch (_) { /* ignore */ }
        throw new Error(`検索エラー: ${res.status}${detail}`);
    }
    return await res.json();
}

/** 生産指示 POST report の report_variant（バックエンドと揃える） */
const PRODUCTION_INSTRUCTION_REPORT_VARIANTS = new Set(['hoikolo', 'ganmono_takiai', 'cab_winna_soti']);

/**
 * @param {{ needDate: string, workcenterIds?: number[], slotCodes?: string[] }} filter
 * @param {number[]} orderIds
 * @param {'hoikolo'|'ganmono_takiai'|'cab_winna_soti'|undefined} reportVariant 省略時は調味液配合表（chomi）
 * @returns {Promise<Blob>}
 */
async function postProductionInstructionReport(filter, orderIds, reportVariant) {
    const wcIds = Array.isArray(filter.workcenterIds) ? filter.workcenterIds : [];
    const slotCodes = Array.isArray(filter.slotCodes) ? filter.slotCodes : [];
    const body = {
        needdate: (filter.needDate && filter.needDate.includes('-'))
            ? filter.needDate.replace(/-/g, '')
            : (filter.needDate || null),
        workcenter_id: wcIds.length ? wcIds : null,
        slot_code: slotCodes.length ? slotCodes : null,
        orderIds: orderIds || []
    };
    if (reportVariant && PRODUCTION_INSTRUCTION_REPORT_VARIANTS.has(reportVariant)) {
        body.report_variant = reportVariant;
    }

    const res = await fetch(`${API_BASE_URL}/production-instruction/report`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body)
    });
    if (!res.ok) {
        const t = await res.text();
        let msg = `PDF出力エラー: ${res.status}`;
        try {
            const j = JSON.parse(t);
            if (j.detail) msg += ' - ' + j.detail;
        } catch (_) { if (t) msg += ' - ' + t; }
        throw new Error(msg);
    }
    return await res.blob();
}

/** 調味液配合表仕様：PDF 出力 */
export function exportProductionInstructionPdf(filter, orderIds) {
    return postProductionInstructionReport(filter, orderIds, undefined);
}

/** 生産指示書_ホイコーロー：PDF 出力 */
export function exportProductionInstructionHoikoloPdf(filter, orderIds) {
    return postProductionInstructionReport(filter, orderIds, 'hoikolo');
}

/** 生産指示書_がんもの炊き合わせ：PDF 出力 */
export function exportProductionInstructionGanmonoTakiaiPdf(filter, orderIds) {
    return postProductionInstructionReport(filter, orderIds, 'ganmono_takiai');
}

/** 生産指示書_キャベツとウィンナーのソティ：PDF 出力 */
export function exportProductionInstructionCabWinnaSotiPdf(filter, orderIds) {
    return postProductionInstructionReport(filter, orderIds, 'cab_winna_soti');
}

/**
 * 検品記録簿：仕入先マスタ一覧（マルチセレクト用）
 * @returns {Promise<Array<{ supplierCode: string, supplierName: string }>>}
 */
export async function fetchInspectionSuppliers() {
    const res = await fetch(`${API_BASE_URL}/inspection-record/suppliers`);
    if (!res.ok) {
        let detail = '';
        try {
            const body = await res.json();
            detail = body.detail ? ` - ${body.detail}` : '';
        } catch (_) { /* ignore */ }
        throw new Error(`仕入先一覧取得エラー: ${res.status}${detail}`);
    }
    return await res.json();
}

/**
 * 検品記録簿：検索（納期必須、仕入先コードは任意の複数）
 * @param {string} needDate
 * @param {string[]} supplierCodes 空配列のとき仕入先で絞り込まない
 */
export async function searchInspectionRecord(needDate, supplierCodes) {
    let needdateStr = needDate;
    if (needDate && needDate.includes('-')) {
        needdateStr = needDate.replace(/-/g, '');
    }
    const params = new URLSearchParams({ needdate: needdateStr });
    (supplierCodes || []).forEach((c) => {
        const v = (c || '').trim();
        if (v) params.append('supplierCodes', v);
    });

    const res = await fetch(`${API_BASE_URL}/inspection-record/search?${params}`);
    if (!res.ok) {
        let detail = '';
        try {
            const body = await res.json();
            detail = body.detail ? ` - ${body.detail}` : '';
        } catch (_) { /* ignore */ }
        throw new Error(`検索エラー: ${res.status}${detail}`);
    }
    return await res.json();
}

/**
 * 検品記録簿：PDF 出力
 */
export async function exportInspectionRecordPdf(filter, lineIds) {
    const body = {
        needdate: (filter.needDate && filter.needDate.includes('-'))
            ? filter.needDate.replace(/-/g, '')
            : (filter.needDate || null),
        lineIds: lineIds || []
    };

    const res = await fetch(`${API_BASE_URL}/inspection-record/report`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body)
    });
    if (!res.ok) {
        const t = await res.text();
        let msg = `PDF出力エラー: ${res.status}`;
        try {
            const j = JSON.parse(t);
            if (j.detail) msg += ' - ' + j.detail;
        } catch (_) { if (t) msg += ' - ' + t; }
        throw new Error(msg);
    }
    return await res.blob();
}

/**
 * 検収の記録簿：得意先マスタ（プルダウン用）
 * @returns {Promise<Array<{ customerCode: string, displayLabel: string }>>}
 */
export async function fetchAcceptanceCustomers() {
    const res = await fetch(`${API_BASE_URL}/acceptance-record/customers`);
    if (!res.ok) {
        let detail = '';
        try {
            const body = await res.json();
            detail = body.detail ? ` - ${body.detail}` : '';
        } catch (_) { /* ignore */ }
        throw new Error(`得意先一覧取得エラー: ${res.status}${detail}`);
    }
    return await res.json();
}

/**
 * 検収の記録簿：納入場所マスタ（マルチセレクト用）
 * @returns {Promise<Array<{ customerCode: string, locationCode: string, displayLabel: string }>>}
 */
export async function fetchAcceptanceDeliveryLocations() {
    const res = await fetch(`${API_BASE_URL}/acceptance-record/delivery-locations`);
    if (!res.ok) {
        let detail = '';
        try {
            const body = await res.json();
            detail = body.detail ? ` - ${body.detail}` : '';
        } catch (_) { /* ignore */ }
        throw new Error(`納入場所一覧取得エラー: ${res.status}${detail}`);
    }
    return await res.json();
}

/**
 * 検収の記録簿：検索（出荷日必須、納品日・店舗は任意）
 * @param {string[]} storePairs customerCode と locationCode をタブで連結した文字列。空配列のとき店舗で絞り込まない
 */
export async function searchAcceptanceRecord(shipDate, deliveryDate, storePairs, customerCode) {
    const shipStr = shipDate && shipDate.includes('-') ? shipDate.replace(/-/g, '') : (shipDate || '').trim();
    const params = new URLSearchParams({ shipdate: shipStr });
    if (deliveryDate && deliveryDate.trim()) {
        const s = deliveryDate.includes('-') ? deliveryDate.replace(/-/g, '') : deliveryDate.trim();
        if (s.length === 8) params.set('deliverydate', s);
    }
    if (customerCode && customerCode.trim()) params.set('customerCode', customerCode.trim());
    (storePairs || []).forEach((pair) => {
        const v = (pair || '').trim();
        if (v) params.append('storePair', v);
    });

    const res = await fetch(`${API_BASE_URL}/acceptance-record/search?${params}`);
    if (!res.ok) {
        let detail = '';
        try {
            const body = await res.json();
            detail = body.detail ? ` - ${body.detail}` : '';
        } catch (_) { /* ignore */ }
        throw new Error(`検索エラー: ${res.status}${detail}`);
    }
    return await res.json();
}

/**
 * 検収の記録簿：PDF 出力
 */
export async function exportAcceptanceRecordPdf(filter, lineIds) {
    const toYyyymmdd = (v) => {
        if (!v) return null;
        return v.includes('-') ? v.replace(/-/g, '') : v;
    };
    const body = {
        deliverydate: toYyyymmdd(filter.deliveryDate),
        shipdate: filter.shipDate ? toYyyymmdd(filter.shipDate) : null,
        lineIds: lineIds || [],
        headerLocation: filter.headerLocation || null,
        headerOutDate: filter.headerOutDate || null,
        headerDelvDate: filter.headerDelvDate || null
    };

    const res = await fetch(`${API_BASE_URL}/acceptance-record/report`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body)
    });
    if (!res.ok) {
        const t = await res.text();
        let msg = `PDF出力エラー: ${res.status}`;
        try {
            const j = JSON.parse(t);
            if (j.detail) msg += ' - ' + j.detail;
        } catch (_) { if (t) msg += ' - ' + t; }
        throw new Error(msg);
    }
    return await res.blob();
}

/**
 * 計量器連携：オーダー検索（着手日範囲は任意）
 * @param {string|null|undefined} releaseDateFrom yyyy-MM-dd
 * @param {string|null|undefined} releaseDateTo yyyy-MM-dd
 */
export async function searchScalesLinkOrders(releaseDateFrom, releaseDateTo) {
    const params = new URLSearchParams();
    if (releaseDateFrom) params.set('releaseDateFrom', releaseDateFrom);
    if (releaseDateTo) params.set('releaseDateTo', releaseDateTo);
    const res = await fetch(`${API_BASE_URL}/scales-link/orders?${params}`);
    if (!res.ok) {
        let detail = '';
        try {
            const body = await res.json();
            detail = body.detail ? ` - ${body.detail}` : '';
        } catch (_) { /* ignore */ }
        throw new Error(`検索エラー: ${res.status}${detail}`);
    }
    return await res.json();
}

/**
 * カット前準備書：作業区マスタ
 */
export async function fetchCutPreparationWorkcenters() {
    const res = await fetch(`${API_BASE_URL}/cut-preparation/workcenters`);
    if (!res.ok) {
        let detail = '';
        try { const body = await res.json(); detail = body.detail ? ` - ${body.detail}` : ''; } catch (_) { /* ignore */ }
        throw new Error(`作業区マスタ取得エラー: ${res.status}${detail}`);
    }
    return await res.json();
}

/**
 * カット前準備書：製造便マスタ（deliveryslot テーブル全件）
 */
export async function fetchCutPreparationManufacturingRoutes() {
    const res = await fetch(`${API_BASE_URL}/cut-preparation/delivery-slots`);
    if (!res.ok) {
        let detail = '';
        try { const body = await res.json(); detail = body.detail ? ` - ${body.detail}` : ''; } catch (_) { /* ignore */ }
        throw new Error(`製造便マスタ取得エラー: ${res.status}${detail}`);
    }
    return await res.json();
}

/**
 * カット前準備書：検索（製造日・製造便・品目コード・作業区 → グループ行）
 */
export async function searchCutPreparation(delvedt, options = {}) {
    let delvedtStr = delvedt;
    if (delvedt && delvedt.includes('-')) delvedtStr = delvedt.replace(/-/g, '');
    const params = new URLSearchParams({ delvedt: delvedtStr });
    const { itemcd, manufacturingRouteCodes = [], workcenterIds = [] } = options;
    (manufacturingRouteCodes || []).forEach(code => {
        const c = code != null ? String(code).trim() : '';
        if (c) params.append('manufacturing_route_code', c);
    });
    (workcenterIds || []).forEach(id => {
        if (id != null && id !== '' && !Number.isNaN(Number(id))) params.append('workcenter_id', String(id));
    });
    if (itemcd && itemcd.trim()) params.set('itemcd', itemcd.trim());
    const res = await fetch(`${API_BASE_URL}/cut-preparation/search?${params}`);
    if (!res.ok) {
        let detail = '';
        try { const body = await res.json(); detail = body.detail ? ` - ${body.detail}` : ''; } catch (_) { /* ignore */ }
        throw new Error(`検索エラー: ${res.status}${detail}`);
    }
    return await res.json();
}

/**
 * カット前準備書：PDF出力
 */
export async function exportCutPreparationPdf(filter, groupKeys) {
    const body = {
        delvedt: filter.delvedt,
        manufacturing_route_codes: filter.manufacturingRouteCodes?.length ? filter.manufacturingRouteCodes : null,
        workcenter_ids: filter.workcenterIds?.length ? filter.workcenterIds : null,
        itemcd: filter.itemcd || null,
        groupKeys: (groupKeys || []).map(k => ({
            delvedt: k.delvedt,
            manufacturingRouteCode: k.manufacturingRouteCode ?? null
        }))
    };
    const res = await fetch(`${API_BASE_URL}/cut-preparation/pdf`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body)
    });
    if (!res.ok) {
        const t = await res.text();
        let msg = `PDF出力エラー: ${res.status}`;
        try { const j = JSON.parse(t); if (j.detail) msg += ' - ' + j.detail; } catch (_) { if (t) msg += ' - ' + t; }
        throw new Error(msg);
    }
    return await res.blob();
}

/**
 * カット前準備書：現品ラベルPDF出力
 */
export async function exportCutPreparationProductLabelPdf(filter, groupKeys, labelCount, instructionType) {
    const body = {
        delvedt: filter.delvedt,
        manufacturing_route_codes: filter.manufacturingRouteCodes?.length ? filter.manufacturingRouteCodes : null,
        workcenter_ids: filter.workcenterIds?.length ? filter.workcenterIds : null,
        itemcd: filter.itemcd || null,
        label_count: labelCount || 1,
        instruction_type: instructionType || 'cut',
        groupKeys: (groupKeys || []).map(k => ({
            delvedt: k.delvedt,
            manufacturingRouteCode: k.manufacturingRouteCode ?? null
        }))
    };
    const res = await fetch(`${API_BASE_URL}/cut-preparation/product-label-pdf`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body)
    });
    if (!res.ok) {
        const t = await res.text();
        let msg = `現品ラベルPDF出力エラー: ${res.status}`;
        try { const j = JSON.parse(t); if (j.detail) msg += ' - ' + j.detail; } catch (_) { if (t) msg += ' - ' + t; }
        throw new Error(msg);
    }
    return await res.blob();
}

/**
 * カット前準備書：Excel出力
 */
export async function exportCutPreparationExcel(filter, groupKeys) {
    const body = {
        delvedt: filter.delvedt,
        manufacturing_route_codes: filter.manufacturingRouteCodes?.length ? filter.manufacturingRouteCodes : null,
        workcenter_ids: filter.workcenterIds?.length ? filter.workcenterIds : null,
        itemcd: filter.itemcd || null,
        groupKeys: (groupKeys || []).map(k => ({
            delvedt: k.delvedt,
            manufacturingRouteCode: k.manufacturingRouteCode ?? null
        }))
    };
    const res = await fetch(`${API_BASE_URL}/cut-preparation/excel`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body)
    });
    if (!res.ok) {
        const t = await res.text();
        let msg = `Excel出力エラー: ${res.status}`;
        try { const j = JSON.parse(t); if (j.detail) msg += ' - ' + j.detail; } catch (_) { if (t) msg += ' - ' + t; }
        throw new Error(msg);
    }
    return await res.blob();
}

/**
 * 予定食数：検索（喫食日・便コード複数）
 * @param {string} delvedt YYYYMMDD
 * @param {string[]} slotCodes
 */
export async function searchYoteiShokusu(delvedt, slotCodes) {
    const params = new URLSearchParams({ delvedt });
    (slotCodes || []).forEach(c => { if (c && String(c).trim()) params.append('slot_code', String(c).trim()); });
    const res = await fetch(`${API_BASE_URL}/yotei-shokusu/search?${params}`);
    if (!res.ok) {
        const t = await res.text();
        let msg = `検索エラー: ${res.status}`;
        try { const j = JSON.parse(t); if (j.detail) msg += ' - ' + j.detail; } catch (_) { if (t) msg += ' - ' + t; }
        throw new Error(msg);
    }
    return await res.json();
}

/**
 * 予定食数：Excel出力 URL を返す（GET エンドポイント）
 * @param {string} delvedt YYYYMMDD
 * @param {string[]} slotCodes
 * @returns {string}
 */
export function buildYoteiShokusuExportUrl(delvedt, slotCodes) {
    const params = new URLSearchParams({ delvedt });
    (slotCodes || []).forEach(c => { if (c && String(c).trim()) params.append('slot_code', String(c).trim()); });
    return `${API_BASE_URL}/yotei-shokusu/export?${params}`;
}

/**
 * 商奉行出力：cstmeat 検索（件数取得）
 * @param {{ slipType: string, dateFrom: string, timeFrom: string, dateTo: string, timeTo: string, customer?: string, store?: string }} p
 * @returns {Promise<{ count: number }>}
 */
export async function searchCstmeat({ slipType, dateFrom, timeFrom, dateTo, timeTo, customer, store } = {}) {
    const params = new URLSearchParams();
    if (slipType) params.set('slip_type', slipType);
    if (dateFrom) params.set('date_from', String(dateFrom).replace(/-/g, ''));
    if (timeFrom) params.set('time_from', String(timeFrom));
    if (dateTo) params.set('date_to', String(dateTo).replace(/-/g, ''));
    if (timeTo) params.set('time_to', String(timeTo));
    if (customer && String(customer).trim()) params.set('customer', String(customer).trim());
    if (store && String(store).trim()) params.set('store', String(store).trim());
    const res = await fetch(`${API_BASE_URL}/cstmeat/search?${params}`);
    if (!res.ok) {
        let detail = '';
        try { const body = await res.json(); detail = body.detail ? ` - ${body.detail}` : ''; } catch (_) { /* ignore */ }
        throw new Error(`検索エラー: ${res.status}${detail}`);
    }
    return await res.json();
}

/**
 * 商奉行出力：cstmeat テキストファイルエクスポート
 * @param {{ slipType: string, dateFrom: string, timeFrom: string, dateTo: string, timeTo: string, customer?: string, store?: string }} p
 * @returns {Promise<Blob>}
 */
export async function exportCstmeatText({ slipType, dateFrom, timeFrom, dateTo, timeTo, customer, store } = {}) {
    const params = new URLSearchParams();
    if (slipType) params.set('slip_type', slipType);
    if (dateFrom) params.set('date_from', String(dateFrom).replace(/-/g, ''));
    if (timeFrom) params.set('time_from', String(timeFrom));
    if (dateTo) params.set('date_to', String(dateTo).replace(/-/g, ''));
    if (timeTo) params.set('time_to', String(timeTo));
    if (customer && String(customer).trim()) params.set('customer', String(customer).trim());
    if (store && String(store).trim()) params.set('store', String(store).trim());
    const res = await fetch(`${API_BASE_URL}/cstmeat/export?${params}`);
    if (!res.ok) {
        let detail = '';
        try { const body = await res.json(); detail = body.detail ? ` - ${body.detail}` : ''; } catch (_) { /* ignore */ }
        throw new Error(`出力エラー: ${res.status}${detail}`);
    }
    const blob = await res.blob();
    const disposition = res.headers.get('Content-Disposition') ?? '';
    const starMatch = disposition.match(/filename\*=UTF-8''([^;]+)/i);
    const plainMatch = disposition.match(/filename="?([^";\r\n]+)"?/i);
    const filename = starMatch
        ? decodeURIComponent(starMatch[1])
        : (plainMatch ? plainMatch[1] : 'export.txt');
    return { blob, filename };
}
