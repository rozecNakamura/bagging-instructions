import { exportInspectionRecordPdf } from './api.js';
import { openPdfInIframe } from './pdf_generator.js';
import { getSelectedInspectionOrderTableIds } from './inspection_record_search.js';

document.addEventListener('DOMContentLoaded', () => {
    const printBtn = document.getElementById('inspectionPrintBtn');
    if (!printBtn) return;

    printBtn.addEventListener('click', async () => {
        const lineIds = getSelectedInspectionOrderTableIds();
        if (!lineIds.length) {
            alert('印刷するデータを選択してください');
            return;
        }

        const needDate = document.getElementById('inspectionNeedDate').value;

        if (!needDate) {
            alert('納期を入力してください');
            return;
        }

        const filter = { needDate };

        try {
            const blob = await exportInspectionRecordPdf(filter, lineIds);
            openPdfInIframe(blob, '検品記録簿 PDF 印刷');
        } catch (e) {
            alert('PDF出力に失敗しました: ' + e.message);
            console.error(e);
        }
    });
});

