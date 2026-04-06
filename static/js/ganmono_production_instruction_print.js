import { exportProductionInstructionGanmonoTakiaiPdf } from './api.js';
import { openPdfInIframe } from './pdf_generator.js';
import { getGmtSelectedOrderIds, getGmtReportFilter } from './ganmono_production_instruction_search.js';

document.addEventListener('DOMContentLoaded', () => {
    const printBtn = document.getElementById('gmtPrintBtn');
    if (!printBtn) return;

    printBtn.addEventListener('click', async () => {
        const orderIds = getGmtSelectedOrderIds();
        if (!orderIds.length) {
            alert('印刷するデータを選択してください');
            return;
        }

        const filter = getGmtReportFilter();

        try {
            const blob = await exportProductionInstructionGanmonoTakiaiPdf(filter, orderIds);
            openPdfInIframe(blob, '生産指示書_がんもの炊き合わせ PDF 印刷');
        } catch (e) {
            alert('PDF出力に失敗しました: ' + e.message);
            console.error(e);
        }
    });
});
