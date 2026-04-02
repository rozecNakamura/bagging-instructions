import { searchCookingInstruction } from './api.js';

let cookRows = [];

function formatYyyymmddToDisplay(yyyymmdd) {
    if (!yyyymmdd || yyyymmdd.length !== 8) return yyyymmdd || '-';
    return `${yyyymmdd.slice(0, 4)}-${yyyymmdd.slice(4, 6)}-${yyyymmdd.slice(6, 8)}`;
}

function displayCookingResults(rows) {
    const section = document.getElementById('cookResultsSection');
    const printSection = document.getElementById('cookPrintSection');
    const countEl = document.getElementById('cookResultCount');
    const tbody = document.getElementById('cookResultsBody');

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
            <td><input type="checkbox" class="cook-item-checkbox" data-index="${index}"></td>
            <td>${row.workplaceNames || '-'}</td>
            <td>${dateDisplay || '-'}</td>
            <td>${row.slotDisplay || '-'}</td>
            <td>${row.parentItemName || '-'}</td>
        `;
        tr.style.cursor = 'pointer';
        tr.addEventListener('click', (e) => {
            if (e.target.classList.contains('cook-item-checkbox')) return;
            const cb = tr.querySelector('.cook-item-checkbox');
            if (cb) cb.checked = !cb.checked;
        });
    });

    section.style.display = 'block';
    printSection.style.display = 'block';
    const headerCheckbox = document.getElementById('cookHeaderCheckbox');
    if (headerCheckbox) headerCheckbox.checked = false;
}

export function getSelectedCookingLineIds() {
    const checked = document.querySelectorAll('.cook-item-checkbox:checked');
    const ids = [];
    checked.forEach(cb => {
        const index = Number(cb.dataset.index);
        const row = cookRows[index];
        if (row && typeof row.salesOrderLineId === 'number') {
            ids.push(row.salesOrderLineId);
        }
    });
    return ids;
}

document.addEventListener('DOMContentLoaded', () => {
    const searchBtn = document.getElementById('cookSearchBtn');
    const headerCheckbox = document.getElementById('cookHeaderCheckbox');
    const selectAllBtn = document.getElementById('cookSelectAllBtn');
    const deselectAllBtn = document.getElementById('cookDeselectAllBtn');

    if (!searchBtn) return;

    searchBtn.addEventListener('click', async () => {
        const needDate = document.getElementById('cookNeedDate').value;
        const workplace = document.getElementById('cookWorkplace').value || '';
        const slot = document.getElementById('cookSlot').value || '';

        if (!needDate) {
            alert('納期を入力してください');
            return;
        }

        try {
            const res = await searchCookingInstruction(needDate, workplace, slot);
            cookRows = res.rows || [];
            displayCookingResults(cookRows);
        } catch (e) {
            alert('検索に失敗しました: ' + e.message);
            console.error(e);
        }
    });

    headerCheckbox?.addEventListener('change', (e) => {
        const checked = e.target.checked;
        document.querySelectorAll('.cook-item-checkbox').forEach(cb => {
            cb.checked = checked;
        });
    });

    selectAllBtn?.addEventListener('click', () => {
        document.querySelectorAll('.cook-item-checkbox').forEach(cb => { cb.checked = true; });
        if (headerCheckbox) headerCheckbox.checked = true;
    });

    deselectAllBtn?.addEventListener('click', () => {
        document.querySelectorAll('.cook-item-checkbox').forEach(cb => { cb.checked = false; });
        if (headerCheckbox) headerCheckbox.checked = false;
    });
});

