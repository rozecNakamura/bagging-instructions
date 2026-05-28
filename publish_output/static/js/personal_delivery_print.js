/**
 * 個人配送指示書：PDF出力（個人配送指示書 / 個人配送指示書（集計）を別ボタンで出力）
 */
import { getSelectedPersonalDeliveryRows, getSelectedPersonalDeliveryVariant } from './personal_delivery_search.js';
import { generatePersonalDeliveryDetailPDF, generatePersonalDeliverySummaryPDF } from './pdf_generator.js';

function handlePrint(variant) {
    return async () => {
        const rows = getSelectedPersonalDeliveryRows();
        if (rows.length === 0) {
            alert('印刷する項目を選択してください');
            return;
        }
        const selectedVariant = getSelectedPersonalDeliveryVariant();
        if (selectedVariant !== variant) {
            alert('検索条件の帳票区分と出力ボタンが一致していません');
            return;
        }
        try {
            const generateFn = variant === 'summary'
                ? generatePersonalDeliverySummaryPDF
                : generatePersonalDeliveryDetailPDF;
            await generateFn(rows);
        } catch (error) {
            alert('印刷に失敗しました: ' + error.message);
        }
    };
}

document.getElementById('personalDeliveryDetailPrintBtn').addEventListener('click', handlePrint('detail'));
document.getElementById('personalDeliverySummaryPrintBtn').addEventListener('click', handlePrint('summary'));
