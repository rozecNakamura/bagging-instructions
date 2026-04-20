import { searchProductionInstruction } from './api.js';

let cwsRows = [];

const screenSel = '#screen-production-instruction-cab-winna-soti';

function displayCwsResults(rows) {
    const section = document.getElementById('cwsResultsSection');
    const printSection = document.getElementById('cwsPrintSection');
    const countEl = document.getElementById('cwsResultCount');
    const tbody = document.getElementById('cwsResultsBody');

    if (!section || !printSection || !countEl || !tbody) return;

    if (!rows || !rows.length) {
        alert('該当するデータが見つかりませんでした');
        section.style.display = 'none';
        printSection.style.display = 'none';
        return;
    }

    countEl.textContent = `${rows.length}件`;
    tbody.innerHTML = '';

    rows.forEach((row, index) => {
        const tr = tbody.insertRow();
        tr.innerHTML = `
            <td><input type="checkbox" class="cws-item-checkbox" data-index="${index}"></td>
            <td>${row.itemCode || '-'}</td>
            <td>${row.itemName || '-'}</td>
            <td>${row.quantityDisplay != null && row.quantityDisplay !== '' ? row.quantityDisplay : '-'}</td>
            <td>${row.unitName || '-'}</td>
        `;
        tr.style.cursor = 'pointer';
        tr.addEventListener('click', (e) => {
            if (e.target.classList.contains('cws-item-checkbox')) return;
            const cb = tr.querySelector('.cws-item-checkbox');
            if (cb) cb.checked = !cb.checked;
        });
    });

    section.style.display = 'block';
    printSection.style.display = 'flex';
    const headerCheckbox = document.getElementById('cwsHeaderCheckbox');
    if (headerCheckbox) headerCheckbox.checked = false;
}

export function getCwsSelectedOrderIds() {
    const checked = document.querySelectorAll(`${screenSel} .cws-item-checkbox:checked`);
    const ids = [];
    checked.forEach(cb => {
        const index = Number(cb.dataset.index);
        const row = cwsRows[index];
        if (row && typeof row.orderTableId === 'number') {
            ids.push(row.orderTableId);
        }
    });
    return ids;
}

/** @returns {{ needDate: string, workcenterIds: number[], slotCodes: string[] }} */
export function getCwsReportFilter() {
    const needDate = document.getElementById('cwsNeedDate')?.value || '';
    return {
        needDate,
        workcenterIds: [],
        slotCodes: []
    };
}

document.addEventListener('DOMContentLoaded', () => {
    const searchBtn = document.getElementById('cwsSearchBtn');
    const headerCheckbox = document.getElementById('cwsHeaderCheckbox');
    const selectAllBtn = document.getElementById('cwsSelectAllBtn');
    const deselectAllBtn = document.getElementById('cwsDeselectAllBtn');

    if (!searchBtn) return;

    searchBtn.addEventListener('click', async () => {
        const needDate = document.getElementById('cwsNeedDate').value;
        const itemQuery = document.getElementById('cwsItemSearch')?.value || '';

        if (!needDate) {
            alert('納期を入力してください');
            return;
        }

        try {
            const res = await searchProductionInstruction(needDate, [], [], itemQuery);
            cwsRows = res.rows || [];
            displayCwsResults(cwsRows);
        } catch (e) {
            alert('検索に失敗しました: ' + e.message);
            console.error(e);
        }
    });

    headerCheckbox?.addEventListener('change', (e) => {
        const checked = e.target.checked;
        document.querySelectorAll(`${screenSel} .cws-item-checkbox`).forEach(cb => {
            cb.checked = checked;
        });
    });

    selectAllBtn?.addEventListener('click', () => {
        document.querySelectorAll(`${screenSel} .cws-item-checkbox`).forEach(cb => { cb.checked = true; });
        if (headerCheckbox) headerCheckbox.checked = true;
    });

    deselectAllBtn?.addEventListener('click', () => {
        document.querySelectorAll(`${screenSel} .cws-item-checkbox`).forEach(cb => { cb.checked = false; });
        if (headerCheckbox) headerCheckbox.checked = false;
    });
});
