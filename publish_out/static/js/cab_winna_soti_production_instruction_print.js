import { exportProductionInstructionCabWinnaSotiPdf } from './api.js';
import { openPdfInIframe } from './pdf_generator.js';
import { getCwsSelectedOrderIds, getCwsReportFilter } from './cab_winna_soti_production_instruction_search.js';

document.addEventListener('DOMContentLoaded', () => {
    const printBtn = document.getElementById('cwsPrintBtn');
    if (!printBtn) return;

    printBtn.addEventListener('click', async () => {
        const orderIds = getCwsSelectedOrderIds();
        if (!orderIds.length) {
            alert('印刷するデータを選択してください');
            return;
        }

        const filter = getCwsReportFilter();

        try {
            const blob = await exportProductionInstructionCabWinnaSotiPdf(filter, orderIds);
            openPdfInIframe(blob, '生産指示書_キャベツとウィンナーのソティ PDF 印刷');
        } catch (e) {
            alert('PDF出力に失敗しました: ' + e.message);
            console.error(e);
        }
    });
});
