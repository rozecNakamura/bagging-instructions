import { exportCookingInstructionPdf } from './api.js';
import { openPdfInIframe } from './pdf_generator.js';
import { getSelectedCookingOrderTableIds } from './cooking_instruction_search.js';

document.addEventListener('DOMContentLoaded', () => {
    const printBtn = document.getElementById('cookPrintBtn');
    if (!printBtn) return;

    printBtn.addEventListener('click', async () => {
        const orderTableIds = getSelectedCookingOrderTableIds();
        if (!orderTableIds.length) {
            alert('印刷するデータを選択してください');
            return;
        }

        const needDate = document.getElementById('cookNeedDate').value;

        const filter = { needDate };

        try {
            const blob = await exportCookingInstructionPdf(filter, orderTableIds);
            openPdfInIframe(blob, '調理指示書 PDF 印刷');
        } catch (e) {
            alert('PDF出力に失敗しました: ' + e.message);
            console.error(e);
        }
    });
});

