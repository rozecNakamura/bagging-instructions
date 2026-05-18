import { openPdfInIframe } from './pdf_generator.js';
import { exportAcceptanceRecordPdf } from './api.js';
import { getSelectedAcceptanceSalesOrderLineIds, buildAcceptanceStoreHeaderText } from './acceptance_record_search.js';

function isoDateToSlash(iso) {
    if (!iso || iso.length < 10) return '';
    const [y, m, d] = iso.split('-');
    return `${y}/${m}/${d}`;
}

document.addEventListener('DOMContentLoaded', () => {
    const printBtn = document.getElementById('acceptancePrintBtn');
    if (!printBtn) return;

    printBtn.addEventListener('click', async () => {
        const lineIds = getSelectedAcceptanceSalesOrderLineIds();
        if (!lineIds.length) {
            alert('印刷するデータを選択してください');
            return;
        }

        const deliveryDate = document.getElementById('acceptanceDelvDate').value;
        if (!deliveryDate) {
            alert('納品日を入力してください');
            return;
        }

        const shipDate = document.getElementById('acceptanceShipDate').value || '';
        const headerLocation = buildAcceptanceStoreHeaderText();

        const filter = {
            deliveryDate,
            shipDate,
            headerLocation,
            headerOutDate: shipDate ? isoDateToSlash(shipDate) : '',
            headerDelvDate: isoDateToSlash(deliveryDate)
        };

        try {
            const blob = await exportAcceptanceRecordPdf(filter, lineIds);
            openPdfInIframe(blob, '検収の記録簿 PDF 印刷');
        } catch (e) {
            alert('PDF出力に失敗しました: ' + e.message);
            console.error(e);
        }
    });
});
