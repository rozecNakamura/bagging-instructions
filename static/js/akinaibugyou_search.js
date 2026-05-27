/**
 * 商奉行出力：検索・テキスト出力
 */
import { searchCstmeat, exportCstmeatText } from './api.js';

let lastSearchParams = null;

function getFormParams() {
    const slipType = document.querySelector('input[name="akinaibugyouSlipType"]:checked')?.value || '';
    const dateFrom = document.getElementById('akinaibugyouDateFrom')?.value || '';
    const timeFrom = document.getElementById('akinaibugyouTimeFrom')?.value || '';
    const dateTo = document.getElementById('akinaibugyouDateTo')?.value || '';
    const timeTo = document.getElementById('akinaibugyouTimeTo')?.value || '';
    const customer = document.getElementById('akinaibugyouCustomer')?.value || '';
    const store = document.getElementById('akinaibugyouStore')?.value || '';
    return { slipType, dateFrom, timeFrom, dateTo, timeTo, customer, store };
}

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
    const searchBtn = document.getElementById('akinaibugyouSearchBtn');
    if (!searchBtn) return;

    const exportBtn = document.getElementById('akinaibugyouExportBtn');
    const exportSection = document.getElementById('akinaibugyouExportSection');
    const resultsSection = document.getElementById('akinaibugyouResultsSection');
    const countEl = document.getElementById('akinaibugyouResultCount');

    searchBtn.addEventListener('click', async () => {
        const params = getFormParams();

        if (!params.dateFrom) {
            alert('納品日（開始）を入力してください');
            return;
        }
        if (!params.dateTo) {
            alert('納品日（終了）を入力してください');
            return;
        }

        const fromKey = params.dateFrom.replace(/-/g, '') + params.timeFrom;
        const toKey = params.dateTo.replace(/-/g, '') + params.timeTo;
        if (fromKey > toKey) {
            alert('開始の納品日・時間帯は終了より前に設定してください');
            return;
        }

        lastSearchParams = params;
        if (exportSection) exportSection.style.display = 'none';
        if (resultsSection) resultsSection.style.display = 'none';

        try {
            const res = await searchCstmeat(params);
            const count = res.count ?? res.total ?? (Array.isArray(res) ? res.length : 0);
            if (countEl) countEl.textContent = `${count}件`;
            if (resultsSection) resultsSection.style.display = 'block';
            if (exportSection) exportSection.style.display = count > 0 ? 'flex' : 'none';
            if (count === 0) alert('該当するデータが見つかりませんでした');
        } catch (e) {
            alert('検索に失敗しました: ' + e.message);
            console.error(e);
        }
    });

    exportBtn?.addEventListener('click', async () => {
        if (!lastSearchParams) {
            alert('先に検索してください');
            return;
        }
        try {
            const blob = await exportCstmeatText(lastSearchParams);
            const dateFrom = (lastSearchParams.dateFrom || '').replace(/-/g, '');
            const dateTo = (lastSearchParams.dateTo || '').replace(/-/g, '');
            triggerDownload(blob, `商奉行出力_${dateFrom}_${dateTo}.txt`);
        } catch (e) {
            alert('出力に失敗しました: ' + e.message);
            console.error(e);
        }
    });
});
