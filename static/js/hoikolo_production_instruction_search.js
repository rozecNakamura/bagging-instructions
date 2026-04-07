import { searchProductionInstruction } from './api.js';

let hclRows = [];

const screenSel = '#screen-production-instruction-hoikolo';

function displayHclResults(rows) {
    const section = document.getElementById('hclResultsSection');
    const printSection = document.getElementById('hclPrintSection');
    const countEl = document.getElementById('hclResultCount');
    const tbody = document.getElementById('hclResultsBody');

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
            <td><input type="checkbox" class="hcl-item-checkbox" data-index="${index}"></td>
            <td>${row.itemCode || '-'}</td>
            <td>${row.itemName || '-'}</td>
            <td>${row.quantityDisplay != null && row.quantityDisplay !== '' ? row.quantityDisplay : '-'}</td>
            <td>${row.unitName || '-'}</td>
        `;
        tr.style.cursor = 'pointer';
        tr.addEventListener('click', (e) => {
            if (e.target.classList.contains('hcl-item-checkbox')) return;
            const cb = tr.querySelector('.hcl-item-checkbox');
            if (cb) cb.checked = !cb.checked;
        });
    });

    section.style.display = 'block';
    printSection.style.display = 'block';
    const headerCheckbox = document.getElementById('hclHeaderCheckbox');
    if (headerCheckbox) headerCheckbox.checked = false;
}

export function getHclSelectedOrderIds() {
    const checked = document.querySelectorAll(`${screenSel} .hcl-item-checkbox:checked`);
    const ids = [];
    checked.forEach(cb => {
        const index = Number(cb.dataset.index);
        const row = hclRows[index];
        if (row && typeof row.orderTableId === 'number') {
            ids.push(row.orderTableId);
        }
    });
    return ids;
}

/** @returns {{ needDate: string, workcenterIds: number[], slotCodes: string[] }} */
export function getHclReportFilter() {
    const needDate = document.getElementById('hclNeedDate')?.value || '';
    return {
        needDate,
        workcenterIds: [],
        slotCodes: []
    };
}

document.addEventListener('DOMContentLoaded', () => {
    const searchBtn = document.getElementById('hclSearchBtn');
    const headerCheckbox = document.getElementById('hclHeaderCheckbox');
    const selectAllBtn = document.getElementById('hclSelectAllBtn');
    const deselectAllBtn = document.getElementById('hclDeselectAllBtn');

    if (!searchBtn) return;

    searchBtn.addEventListener('click', async () => {
        const needDate = document.getElementById('hclNeedDate').value;
        const itemQuery = document.getElementById('hclItemSearch')?.value || '';

        if (!needDate) {
            alert('納期を入力してください');
            return;
        }

        try {
            const res = await searchProductionInstruction(needDate, [], [], itemQuery);
            hclRows = res.rows || [];
            displayHclResults(hclRows);
        } catch (e) {
            alert('検索に失敗しました: ' + e.message);
            console.error(e);
        }
    });

    headerCheckbox?.addEventListener('change', (e) => {
        const checked = e.target.checked;
        document.querySelectorAll(`${screenSel} .hcl-item-checkbox`).forEach(cb => {
            cb.checked = checked;
        });
    });

    selectAllBtn?.addEventListener('click', () => {
        document.querySelectorAll(`${screenSel} .hcl-item-checkbox`).forEach(cb => { cb.checked = true; });
        if (headerCheckbox) headerCheckbox.checked = true;
    });

    deselectAllBtn?.addEventListener('click', () => {
        document.querySelectorAll(`${screenSel} .hcl-item-checkbox`).forEach(cb => { cb.checked = false; });
        if (headerCheckbox) headerCheckbox.checked = false;
    });
});
