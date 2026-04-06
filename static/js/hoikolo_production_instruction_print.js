import { exportProductionInstructionHoikoloPdf } from './api.js';
import { openPdfInIframe } from './pdf_generator.js';
import { getHclSelectedOrderIds, getHclReportFilter } from './hoikolo_production_instruction_search.js';

document.addEventListener('DOMContentLoaded', () => {
    const printBtn = document.getElementById('hclPrintBtn');
    if (!printBtn) return;

    printBtn.addEventListener('click', async () => {
        const orderIds = getHclSelectedOrderIds();
        if (!orderIds.length) {
            alert('印刷するデータを選択してください');
            return;
        }

        const filter = getHclReportFilter();

        try {
            const blob = await exportProductionInstructionHoikoloPdf(filter, orderIds);
            openPdfInIframe(blob, '生産指示書_ホイコーロー PDF 印刷');
        } catch (e) {
            alert('PDF出力に失敗しました: ' + e.message);
            console.error(e);
        }
    });
});
