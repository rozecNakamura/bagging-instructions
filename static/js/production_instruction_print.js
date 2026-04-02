import { exportProductionInstructionPdf } from './api.js';
import { openPdfInIframe } from './pdf_generator.js';
import { getSelectedOrderIds } from './production_instruction_search.js';

document.addEventListener('DOMContentLoaded', () => {
    const printBtn = document.getElementById('prodPrintBtn');
    if (!printBtn) return;

    printBtn.addEventListener('click', async () => {
        const orderIds = getSelectedOrderIds();
        if (!orderIds.length) {
            alert('印刷するデータを選択してください');
            return;
        }

        const needDate = document.getElementById('prodNeedDate').value;
        const workcenter = document.getElementById('prodWorkcenter').value || '';
        const slot = document.getElementById('prodSlot').value || '';

        const filter = { needDate, workcenter, slot };

        try {
            const blob = await exportProductionInstructionPdf(filter, orderIds);
            openPdfInIframe(blob, '製造指示書 PDF 印刷');
        } catch (e) {
            alert('PDF出力に失敗しました: ' + e.message);
            console.error(e);
        }
    });
});

