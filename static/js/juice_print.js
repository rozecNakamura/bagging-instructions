/**
 * 汁仕分表：PDF印刷
 */
import { getSelectedJuiceRows } from './juice_search.js';
import { generateJuicePDF } from './pdf_generator.js';

document.getElementById('juicePrintBtn').addEventListener('click', async () => {
    const rows = getSelectedJuiceRows();
    if (rows.length === 0) {
        alert('印刷する項目を選択してください');
        return;
    }
    try {
        await generateJuicePDF(rows);
    } catch (error) {
        alert('印刷に失敗しました: ' + error.message);
    }
});
