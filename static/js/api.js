/**
 * API通信処理
 */

const API_BASE_URL = '/api';

/**
 * 受注明細を検索
 */
export async function searchOrders(productionDate, productCode) {
    try {
        // 日付をYYYYMMDD形式に変換（YYYY-MM-DD → YYYYMMDD）
        let prddt = productionDate;
        if (productionDate && productionDate.includes('-')) {
            prddt = productionDate.replace(/-/g, ''); // ハイフンを削除
        }
        
        const params = new URLSearchParams({
            prddt: prddt,
            itemcd: productCode
        });
        
        const response = await fetch(`${API_BASE_URL}/search?${params}`);
        
        if (!response.ok) {
            let detail = '';
            try {
                const body = await response.json();
                detail = body.detail ? ` - ${body.detail}` : '';
            } catch (_) { /* body が JSON でない場合は無視 */ }
            throw new Error(`検索エラー: ${response.status}${detail}`);
        }
        
        return await response.json();
    } catch (error) {
        console.error('検索APIエラー:', error);
        throw error;
    }
}

/**
 * 汁仕分表 PDF 生成（rxz テンプレート使用・サーバー側で PDF 生成）
 * @param {Array<{ delvedt: string, shptmDisplay: string, jobordmernm: string, shpctrnm: string, jobordqun: number, addinfo02: string }>} rows
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
                addinfo02: r.addinfo02
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
 * 弁当箱盛り付け指示書（ご飯）PDF 生成（rxz テンプレート使用・サーバー側で PDF 生成）
 * GRAM=jobordqun, PACK=quantity/addinfo02, LOCATIONNM=なし
 * @param {Array<{ delvedt: string, shptmDisplay: string, jobordmernm: string, jobordqun: number, quantity: number, addinfo02: string }>} rows
 * @returns {Promise<Blob>}
 */
export async function generateBentoPdfBlob(rows) {
    const response = await fetch(`${API_BASE_URL}/bento/pdf`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            rows: rows.map(r => ({
                delvedt: r.delvedt,
                shptmDisplay: r.shptmDisplay,
                jobordmernm: r.jobordmernm,
                jobordqun: r.jobordqun,
                quantity: r.quantity ?? 0,
                addinfo02: r.addinfo02 ?? ''
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
export async function searchJuice(delvedt, itemcd) {
    try {
        let delvedtStr = delvedt;
        if (delvedt && delvedt.includes('-')) {
            delvedtStr = delvedt.replace(/-/g, '');
        }
        const params = new URLSearchParams({
            delvedt: delvedtStr,
            itemcd: itemcd || ''
        });
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
export async function searchDeliveryNote(delvedt) {
    try {
        let delvedtStr = delvedt;
        if (delvedt && delvedt.includes('-')) {
            delvedtStr = delvedt.replace(/-/g, '');
        }
        const params = new URLSearchParams({ delvedt: delvedtStr });
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
export async function fetchMiddleClassifications(majorId) {
    const params = new URLSearchParams({ majorclassificationid: String(majorId) });
    const res = await fetch(`${API_BASE_URL}/preparation-work/middle-classifications?${params}`);
    if (!res.ok) {
        throw new Error(`中分類取得エラー: ${res.status}`);
    }
    return await res.json();
}

/**
 * 作業前準備書：検索（納期・便・品目・大分類・中分類 → グループ行）
 */
export async function searchPreparationWork(delvedt, slot, itemcd, majorId, middleId) {
    let delvedtStr = delvedt;
    if (delvedt && delvedt.includes('-')) {
        delvedtStr = delvedt.replace(/-/g, '');
    }
    const params = new URLSearchParams({ delvedt: delvedtStr });
    if (slot && slot.trim()) params.set('slot', slot.trim());
    if (itemcd && itemcd.trim()) params.set('itemcd', itemcd.trim());
    if (majorId) params.set('majorclassificationid', String(majorId));
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
        slot: filter.slot || null,
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
        slot: filter.slot || null,
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
 * 弁当箱盛り付け指示書（ご飯）用：喫食日・品目コードで検索。itemadditionalinformation.addinfo01 が存在する品目のみ。
 */
export async function searchBento(delvedt, itemcd) {
    try {
        let delvedtStr = delvedt;
        if (delvedt && delvedt.includes('-')) {
            delvedtStr = delvedt.replace(/-/g, '');
        }
        const params = new URLSearchParams({
            delvedt: delvedtStr,
            itemcd: itemcd || ''
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
 * 個人配送指示書用：配送日で検索（配送日・喫食時間・配送エリア）
 */
export async function searchPersonalDelivery(delvedt) {
    try {
        let delvedtStr = delvedt;
        if (delvedt && delvedt.includes('-')) {
            delvedtStr = delvedt.replace(/-/g, '');
        }
        const params = new URLSearchParams({ delvedt: delvedtStr });
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
 * @param {Array<{ delivery_date: string, time_name: string, area: string }>} rows
 * @param {'detail'|'summary'} variant - 'detail'=個人配送指示書.rxz のみ / 'summary'=個人配送指示書（集計）.rxz のみ
 * @returns {Promise<Blob>}
 */
export async function generatePersonalDeliveryPdfBlob(rows, variant) {
    const response = await fetch(`${API_BASE_URL}/personal-delivery/pdf`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            rows: rows.map(r => ({
                delivery_date: r.delivery_date,
                time_name: r.time_name ?? '',
                area: r.area ?? ''
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

/**
 * 現品票：ordertable 検索（納期＋任意の大分類）
 * @param {string} needDate - YYYY-MM-DD または YYYYMMDD
 * @param {number|string|null|undefined} majorClassificationId - 未指定・空なら大分類で絞り込まない
 */
export async function searchProductLabel(needDate, majorClassificationId) {
    let needdateStr = needDate;
    if (needDate && needDate.includes('-')) {
        needdateStr = needDate.replace(/-/g, '');
    }
    const params = new URLSearchParams({ needdate: needdateStr });
    if (majorClassificationId != null && String(majorClassificationId).trim() !== '') {
        params.set('majorclassificationid', String(majorClassificationId));
    }
    const response = await fetch(`${API_BASE_URL}/product-label/search?${params}`);
    if (!response.ok) {
        let detail = '';
        try {
            const body = await response.json();
            detail = body.detail ? ` - ${body.detail}` : '';
        } catch (_) { /* ignore */ }
        throw new Error(`検索エラー: ${response.status}${detail}`);
    }
    return await response.json();
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
export async function calculateBagging(jobordPrkeys, printType) {
    try {
        const response = await fetch(`${API_BASE_URL}/bagging/calculate`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                jobord_prkeys: jobordPrkeys,
                print_type: printType
            })
        });
        
        if (!response.ok) {
            let detail = '';
            try {
                const body = await response.json();
                detail = body.detail ? ` - ${body.detail}` : '';
            } catch (_) { /* body が JSON でない場合は無視 */ }
            throw new Error(`計算エラー: ${response.status}${detail}`);
        }
        
        const data = await response.json();
        
        return data;
    } catch (error) {
        console.error('計算APIエラー:', error);
        throw error;
    }
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
 * 調理指示書：便マスタ
 * @returns {Promise<{ code: string, name: string }[]>}
 */
export async function fetchCookingSlots() {
    const res = await fetch(`${API_BASE_URL}/cooking-instruction/slots`);
    if (!res.ok) {
        let detail = '';
        try {
            const body = await res.json();
            detail = body.detail ? ` - ${body.detail}` : '';
        } catch (_) { /* ignore */ }
        throw new Error(`便マスタ取得エラー: ${res.status}${detail}`);
    }
    return await res.json();
}

/**
 * 調理指示書：検索（納期・作業区 ID 複数・便コード複数）
 * @param {string} needDate
 * @param {number[]} workcenterIds
 * @param {string[]} slotCodes
 */
export async function searchCookingInstruction(needDate, workcenterIds, slotCodes) {
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
export async function searchSortingInquiry(delvedt, slotCodes) {
    let delvedtStr = delvedt;
    if (delvedt && delvedt.includes('-')) {
        delvedtStr = delvedt.replace(/-/g, '');
    }
    const params = new URLSearchParams({ delvedt: delvedtStr });
    const codes = Array.isArray(slotCodes) ? slotCodes : [];
    codes.forEach(c => {
        if (c && String(c).trim()) params.append('slot_code', String(c).trim());
    });
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
export async function exportSortingInquiryShiwakeBlob(delvedt, slotCodes) {
    let delvedtStr = delvedt;
    if (delvedt && delvedt.includes('-')) {
        delvedtStr = delvedt.replace(/-/g, '');
    }
    const params = new URLSearchParams({ delvedt: delvedtStr });
    (Array.isArray(slotCodes) ? slotCodes : []).forEach(c => {
        if (c && String(c).trim()) params.append('slot_code', String(c).trim());
    });
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
export async function exportSortingInquiryJournalBlob(delvedt, slotCodes) {
    let delvedtStr = delvedt;
    if (delvedt && delvedt.includes('-')) {
        delvedtStr = delvedt.replace(/-/g, '');
    }
    const params = new URLSearchParams({ delvedt: delvedtStr });
    (Array.isArray(slotCodes) ? slotCodes : []).forEach(c => {
        if (c && String(c).trim()) params.append('slot_code', String(c).trim());
    });
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

export async function fetchProductionInstructionSlots() {
    const res = await fetch(`${API_BASE_URL}/production-instruction/slots`);
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
 * 検収の記録簿：検索（納品日必須、出荷日・店舗は任意）
 * @param {string[]} storePairs customerCode と locationCode をタブで連結した文字列。空配列のとき店舗で絞り込まない
 */
export async function searchAcceptanceRecord(deliveryDate, shipDate, storePairs) {
    let delvStr = deliveryDate;
    if (deliveryDate && deliveryDate.includes('-')) {
        delvStr = deliveryDate.replace(/-/g, '');
    }
    const params = new URLSearchParams({ deliverydate: delvStr });
    if (shipDate && shipDate.trim()) {
        const s = shipDate.includes('-') ? shipDate.replace(/-/g, '') : shipDate.trim();
        if (s.length === 8) params.set('shipdate', s);
    }
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

