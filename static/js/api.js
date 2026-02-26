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
 * 受注明細詳細を取得（全リレーションデータ含む）
 * 印刷処理の前に呼び出す
 */
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

