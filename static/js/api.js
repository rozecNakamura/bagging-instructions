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
            throw new Error(`検索エラー: ${response.status}`);
        }
        
        return await response.json();
    } catch (error) {
        console.error('検索APIエラー:', error);
        throw error;
    }
}

/**
 * 袋詰指示書・ラベルデータを計算
 */
export async function calculateBagging(jobordIds, printType) {
    try {
        const response = await fetch(`${API_BASE_URL}/bagging/calculate`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                jobord_ids: jobordIds,
                print_type: printType
            })
        });
        
        if (!response.ok) {
            throw new Error(`計算エラー: ${response.status}`);
        }
        
        return await response.json();
    } catch (error) {
        console.error('計算APIエラー:', error);
        throw error;
    }
}

