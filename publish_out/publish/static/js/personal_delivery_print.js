/**
 * 個人配送指示書：PDF出力（個人配送指示書 / 個人配送指示書（集計）を別ボタンで出力）
 */
import { getSelectedPersonalDeliveryRows } from './personal_delivery_search.js';
import { generatePersonalDeliveryDetailPDF, generatePersonalDeliverySummaryPDF } from './pdf_generator.js';

function handlePrint(generateFn) {
    return async () => {
        const rows = getSelectedPersonalDeliveryRows();
        if (rows.length === 0) {
            alert('印刷する項目を選択してください');
            return;
        }
        try {
            await generateFn(rows);
        } catch (error) {
            alert('印刷に失敗しました: ' + error.message);
        }
    };
}

document.getElementById('personalDeliveryDetailPrintBtn').addEventListener('click', handlePrint(generatePersonalDeliveryDetailPDF));
document.getElementById('personalDeliverySummaryPrintBtn').addEventListener('click', handlePrint(generatePersonalDeliverySummaryPDF));
