/**
 * 仕分け照会：Excel 出力
 */
import {
    exportSortingInquiryShiwakeBlob,
    exportSortingInquiryJournalBlob
} from './api.js';
import { getSortingInquiryExportParams } from './sorting_inquiry_search.js';

function triggerDownload(blob, filename) {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.rel = 'noopener';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}

document.addEventListener('DOMContentLoaded', () => {
    const shiwakeBtn = document.getElementById('siExportShiwakeBtn');
    const journalBtn = document.getElementById('siExportJournalBtn');

    shiwakeBtn?.addEventListener('click', async () => {
        const { delvedt, slotCodes } = getSortingInquiryExportParams();
        if (!delvedt || delvedt.length !== 8) {
            alert('先に喫食日を入力し、検索してください');
            return;
        }
        try {
            const blob = await exportSortingInquiryShiwakeBlob(delvedt, slotCodes);
            triggerDownload(blob, `2_仕分け照会_${delvedt}.xlsx`);
        } catch (e) {
            alert('Excel 出力に失敗しました: ' + e.message);
            console.error(e);
        }
    });

    journalBtn?.addEventListener('click', async () => {
        const { delvedt, slotCodes } = getSortingInquiryExportParams();
        if (!delvedt || delvedt.length !== 8) {
            alert('先に喫食日を入力し、検索してください');
            return;
        }
        try {
            const blob = await exportSortingInquiryJournalBlob(delvedt, slotCodes);
            triggerDownload(blob, `仕訳表自動調整_${delvedt}.xlsx`);
        } catch (e) {
            alert('Excel 出力に失敗しました: ' + e.message);
            console.error(e);
        }
    });
});
