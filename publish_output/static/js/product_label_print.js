/**
 * 現品票（調理）：サーバー PDF（現品票（調理）.rxz）→ブラウザ印刷
 */
import { generateProductLabelPdfBlob } from './api.js';
import { openPdfInIframe } from './pdf_generator.js';
import { getSelectedProductLabelOrderTableIds } from './product_label_search.js';

document.getElementById('productLabelPrintBtn').addEventListener('click', async () => {
    const ids = getSelectedProductLabelOrderTableIds();
    if (!ids.length) {
        alert('印刷する行にチェックを入れてください。');
        return;
    }

    const instructionType = document.getElementById('productLabelInstructionType')?.value;
    if (!instructionType) {
        alert('指示書種別を選択してください。');
        return;
    }

    const labelCountEl = document.getElementById('productLabelLabelCount');
    const labelCount = labelCountEl ? Math.max(1, parseInt(labelCountEl.value, 10) || 1) : 1;

    const cutModeEl = document.querySelector('input[name="productLabelCutMode"]:checked');
    const cutMode = cutModeEl ? cutModeEl.value : 'no_cut';

    try {
        const blob = await generateProductLabelPdfBlob(ids, labelCount, cutMode, instructionType);
        openPdfInIframe(blob, '現品票（調理） PDF 印刷');
    } catch (e) {
        alert(e instanceof Error ? e.message : String(e));
    }
});
