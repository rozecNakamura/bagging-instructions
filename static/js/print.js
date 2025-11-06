/**
 * 印刷処理
 */

import { calculateBagging } from './api.js';
import { getSelectedPrkeys } from './search.js';
import { generateInstructionPDF, generateLabelPDF } from './pdf_generator.js';

// 印刷ボタンイベント
document.getElementById('printBtn').addEventListener('click', async () => {
    const selectedPrkeys = getSelectedPrkeys();
    
    if (selectedPrkeys.length === 0) {
        alert('印刷する項目を選択してください');
        return;
    }
    
    const printType = document.querySelector('input[name="printType"]:checked').value;
    
    try {
        // 袋詰計算を実行（リレーションデータも含まれる）
        console.log('袋詰計算を実行中（リレーションデータ含む）...');
        const data = await calculateBagging(selectedPrkeys, printType);
        console.log('袋詰計算結果:', data);
        
        if (printType === 'instruction') {
            generateInstructionPDF(data);
        } else {
            generateLabelPDF(data);
        }
    } catch (error) {
        alert('印刷データの取得に失敗しました: ' + error.message);
    }
});

