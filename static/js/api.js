/**
 * API通信処理
 */

const API_BASE_URL = '/api';

/**
 * 受注明細を検索
 */
export async function searchOrders(productionDate, productCode) {
    try {
        const params = new URLSearchParams({
            production_date: productionDate,
            product_code: productCode
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

