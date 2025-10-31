/**
 * 印刷処理
 */

import { calculateBagging } from './api.js';
import { getSelectedIds } from './search.js';
import { generateInstructionPDF, generateLabelPDF } from './pdf_generator.js';

// 印刷ボタンイベント
document.getElementById('printBtn').addEventListener('click', async () => {
    const selectedIds = getSelectedIds();
    
    if (selectedIds.length === 0) {
        alert('印刷する項目を選択してください');
        return;
    }
    
    const printType = document.querySelector('input[name="printType"]:checked').value;
    
    try {
        const data = await calculateBagging(selectedIds, printType);
        
        if (printType === 'instruction') {
            generateInstructionPDF(data);
        } else {
            generateLabelPDF(data);
        }
    } catch (error) {
        alert('印刷データの取得に失敗しました: ' + error.message);
    }
});

