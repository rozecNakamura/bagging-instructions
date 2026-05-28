/**
 * ご飯盛り付け指示書：PDF印刷
 */
import { getSelectedGohanRows } from './gohan_search.js';
import { generateGohanPDF } from './pdf_generator.js';

document.getElementById('gohanPrintBtn').addEventListener('click', async () => {
    const rows = getSelectedGohanRows();
    if (rows.length === 0) {
        alert('印刷する項目を選択してください');
        return;
    }
    try {
        await generateGohanPDF(rows);
    } catch (error) {
        alert('印刷に失敗しました: ' + error.message);
    }
});
