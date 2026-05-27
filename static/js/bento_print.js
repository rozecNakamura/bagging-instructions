/**
 * 弁当箱盛り付け指示書：PDF印刷
 */
import { getSelectedBentoRows } from './bento_search.js';
import { generateBentoPDF } from './pdf_generator.js';

document.getElementById('bentoPrintBtn').addEventListener('click', async () => {
    const { rows, bentoType } = getSelectedBentoRows();
    if (rows.length === 0) {
        alert('印刷する項目を選択してください');
        return;
    }
    try {
        await generateBentoPDF(rows, bentoType);
    } catch (error) {
        alert('印刷に失敗しました: ' + error.message);
    }
});
