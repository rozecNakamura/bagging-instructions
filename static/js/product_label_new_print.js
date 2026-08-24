/**
 * 現品票印刷（新）：チェックした子品目行のみをサーバー PDF（現品票（調理）1枚.rxz）で出力→ブラウザ印刷。
 */
import { generateProductLabelNewPdfBlob } from './api.js';
import { openLabelPdfForPrint } from './pdf_generator.js';
import { getSelectedProductLabelNewItems } from './product_label_new_search.js';

document.getElementById('productLabelNewPrintBtn')?.addEventListener('click', async () => {
    const items = getSelectedProductLabelNewItems();
    if (!items.length) {
        alert('印刷する行にチェックを入れてください。');
        return;
    }

    const instructionType = document.getElementById('productLabelNewInstructionType')?.value;
    if (!instructionType) {
        alert('指示書種別を選択してください。');
        return;
    }

    const cutModeEl = document.querySelector('input[name="productLabelNewCutMode"]:checked');
    const cutMode = cutModeEl ? cutModeEl.value : 'no_cut';

    try {
        const blob = await generateProductLabelNewPdfBlob(items, instructionType, cutMode);
        // ラベル専用: 60×60mm ページを正しいサイズで印刷するため、
        // 新しいウィンドウで PDF を開いて印刷ダイアログを起動する。
        // 印刷ダイアログで用紙サイズを「60×60mm」に、倍率を「実際のサイズ」に設定してください。
        openLabelPdfForPrint(blob, '現品票（調理）1枚 PDF 印刷');
    } catch (e) {
        alert(e instanceof Error ? e.message : String(e));
    }
});
