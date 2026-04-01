import { searchAggregateSummary, exportAggregateSummaryPdf } from './api.js';
import { openPdfInIframe } from './pdf_generator.js';

let aggRows = [];

function formatYyyymmddToDisplay(yyyymmdd) {
    if (!yyyymmdd || yyyymmdd.length !== 8) return yyyymmdd || '-';
    return `${yyyymmdd.slice(0, 4)}-${yyyymmdd.slice(4, 6)}-${yyyymmdd.slice(6, 8)}`;
}

function getSelectedSummaryKeys() {
    const checked = document.querySelectorAll('.agg-item-checkbox:checked');
    const keys = [];
    checked.forEach(cb => {
        const index = Number(cb.dataset.index);
        const row = aggRows[index];
        if (row && row.key) {
            keys.push(row.key);
        }
    });
    return keys;
}

document.addEventListener('DOMContentLoaded', () => {
    const searchBtn = document.getElementById('aggSearchBtn');
    const headerCheckbox = document.getElementById('aggHeaderCheckbox');
    const selectAllBtn = document.getElementById('aggSelectAllBtn');
    const deselectAllBtn = document.getElementById('aggDeselectAllBtn');
    const printBtn = document.getElementById('aggPrintBtn');

    if (!searchBtn) return;

    searchBtn.addEventListener('click', async () => {
        const fromDate = document.getElementById('aggFromDate').value;
        const toDate = document.getElementById('aggToDate').value;
        const itemCode = document.getElementById('aggItemCode').value || '';
        const majorClass = document.getElementById('aggMajorClass').value || '';
        const middleClass = document.getElementById('aggMiddleClass').value || '';

        if (!fromDate) {
            alert('出庫日Fromを入力してください');
            return;
        }

        try {
            let from = fromDate;
            let to = toDate;
            if (from && from.includes('-')) from = from.replace(/-/g, '');
            if (to && to.includes('-')) to = to.replace(/-/g, '');

            const res = await searchAggregateSummary(from, to, itemCode, majorClass, middleClass);
            aggRows = res.rows || [];
            const section = document.getElementById('aggResultsSection');
            const printSection = document.getElementById('aggPrintSection');

            if (!aggRows.length) {
                alert('該当するデータが見つかりませんでした');
                section.style.display = 'none';
                printSection.style.display = 'none';
                return;
            }

            const countEl = document.getElementById('aggResultCount');
            const tbody = document.getElementById('aggResultsBody');
            countEl.textContent = `${aggRows.length}件`;
            tbody.innerHTML = '';

            aggRows.forEach((row, index) => {
                const tr = tbody.insertRow();
                const dateDisplay = row.shipDate || formatYyyymmddToDisplay(row.key?.shipDate || '');
                tr.innerHTML = `
                    <td><input type="checkbox" class="agg-item-checkbox" data-index="${index}"></td>
                    <td>${dateDisplay || '-'}</td>
                    <td>${row.majorClassificationName || '-'}</td>
                    <td>${row.middleClassificationName || '-'}</td>
                    <td class="col-num">${row.childItemCount ?? 0}</td>
                `;
                tr.style.cursor = 'pointer';
                tr.addEventListener('click', (e) => {
                    if (e.target.classList.contains('agg-item-checkbox')) return;
                    const cb = tr.querySelector('.agg-item-checkbox');
                    if (cb) cb.checked = !cb.checked;
                });
            });

            section.style.display = 'block';
            printSection.style.display = 'block';
            headerCheckbox.checked = false;
        } catch (e) {
            alert('検索に失敗しました: ' + e.message);
            console.error(e);
        }
    });

    headerCheckbox?.addEventListener('change', (e) => {
        const checked = e.target.checked;
        document.querySelectorAll('.agg-item-checkbox').forEach(cb => {
            cb.checked = checked;
        });
    });

    selectAllBtn?.addEventListener('click', () => {
        document.querySelectorAll('.agg-item-checkbox').forEach(cb => { cb.checked = true; });
        if (headerCheckbox) headerCheckbox.checked = true;
    });

    deselectAllBtn?.addEventListener('click', () => {
        document.querySelectorAll('.agg-item-checkbox').forEach(cb => { cb.checked = false; });
        if (headerCheckbox) headerCheckbox.checked = false;
    });

    printBtn?.addEventListener('click', async () => {
        const keys = getSelectedSummaryKeys();
        if (!keys.length) {
            alert('印刷するグループを選択してください');
            return;
        }

        const fromDate = document.getElementById('aggFromDate').value;
        const toDate = document.getElementById('aggToDate').value;
        const itemCode = document.getElementById('aggItemCode').value || '';
        const majorClass = document.getElementById('aggMajorClass').value || '';
        const middleClass = document.getElementById('aggMiddleClass').value || '';

        const filter = {
            fromDate,
            toDate,
            itemCode,
            majorClass,
            middleClass
        };

        try {
            const blob = await exportAggregateSummaryPdf(filter, keys);
            openPdfInIframe(blob, '集計表 PDF 印刷');
        } catch (e) {
            alert('PDF出力に失敗しました: ' + e.message);
            console.error(e);
        }
    });
});

