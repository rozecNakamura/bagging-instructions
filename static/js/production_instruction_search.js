import { searchProductionInstruction } from './api.js';

let prodRows = [];

function displayProductionResults(rows) {
    const section = document.getElementById('prodResultsSection');
    const printSection = document.getElementById('prodPrintSection');
    const countEl = document.getElementById('prodResultCount');
    const tbody = document.getElementById('prodResultsBody');

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
        const dateDisplay = row.needDate || '-';
        tr.innerHTML = `
            <td><input type="checkbox" class="prod-item-checkbox" data-index="${index}"></td>
            <td>${row.itemName || '-'}</td>
            <td>${dateDisplay || '-'}</td>
            <td>${row.slotDisplay || '-'}</td>
        `;
        tr.style.cursor = 'pointer';
        tr.addEventListener('click', (e) => {
            if (e.target.classList.contains('prod-item-checkbox')) return;
            const cb = tr.querySelector('.prod-item-checkbox');
            if (cb) cb.checked = !cb.checked;
        });
    });

    section.style.display = 'block';
    printSection.style.display = 'block';
    const headerCheckbox = document.getElementById('prodHeaderCheckbox');
    if (headerCheckbox) headerCheckbox.checked = false;
}

export function getSelectedOrderIds() {
    const checked = document.querySelectorAll('.prod-item-checkbox:checked');
    const ids = [];
    checked.forEach(cb => {
        const index = Number(cb.dataset.index);
        const row = prodRows[index];
        if (row && typeof row.orderTableId === 'number') {
            ids.push(row.orderTableId);
        }
    });
    return ids;
}

document.addEventListener('DOMContentLoaded', () => {
    const searchBtn = document.getElementById('prodSearchBtn');
    const headerCheckbox = document.getElementById('prodHeaderCheckbox');
    const selectAllBtn = document.getElementById('prodSelectAllBtn');
    const deselectAllBtn = document.getElementById('prodDeselectAllBtn');

    if (!searchBtn) return;

    searchBtn.addEventListener('click', async () => {
        const needDate = document.getElementById('prodNeedDate').value;
        const workcenter = document.getElementById('prodWorkcenter').value || '';
        const slot = document.getElementById('prodSlot').value || '';

        if (!needDate) {
            alert('納期を入力してください');
            return;
        }

        try {
            const res = await searchProductionInstruction(needDate, workcenter, slot);
            prodRows = res.rows || [];
            displayProductionResults(prodRows);
        } catch (e) {
            alert('検索に失敗しました: ' + e.message);
            console.error(e);
        }
    });

    headerCheckbox?.addEventListener('change', (e) => {
        const checked = e.target.checked;
        document.querySelectorAll('.prod-item-checkbox').forEach(cb => {
            cb.checked = checked;
        });
    });

    selectAllBtn?.addEventListener('click', () => {
        document.querySelectorAll('.prod-item-checkbox').forEach(cb => { cb.checked = true; });
        if (headerCheckbox) headerCheckbox.checked = true;
    });

    deselectAllBtn?.addEventListener('click', () => {
        document.querySelectorAll('.prod-item-checkbox').forEach(cb => { cb.checked = false; });
        if (headerCheckbox) headerCheckbox.checked = false;
    });
});

