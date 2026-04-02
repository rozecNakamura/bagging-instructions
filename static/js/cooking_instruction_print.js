import { exportCookingInstructionPdf } from './api.js';
import { openPdfInIframe } from './pdf_generator.js';
import { getSelectedCookingLineIds } from './cooking_instruction_search.js';

document.addEventListener('DOMContentLoaded', () => {
    const printBtn = document.getElementById('cookPrintBtn');
    if (!printBtn) return;

    printBtn.addEventListener('click', async () => {
        const lineIds = getSelectedCookingLineIds();
        if (!lineIds.length) {
            alert('印刷するデータを選択してください');
            return;
        }

        const needDate = document.getElementById('cookNeedDate').value;
        const workplace = document.getElementById('cookWorkplace').value || '';
        const slot = document.getElementById('cookSlot').value || '';

        const filter = { needDate, workplace, slot };

        try {
            const blob = await exportCookingInstructionPdf(filter, lineIds);
            openPdfInIframe(blob, '調理指示書 PDF 印刷');
        } catch (e) {
            alert('PDF出力に失敗しました: ' + e.message);
            console.error(e);
        }
    });
});

