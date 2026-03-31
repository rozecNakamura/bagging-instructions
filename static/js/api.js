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

