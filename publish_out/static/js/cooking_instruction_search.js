import { searchCookingInstruction, fetchCookingWorkcenters, fetchCookingSlots, fetchCookingManufacturingRoutes } from './api.js';

let cookRows = [];
let cookWorkcenterList = [];
let cookSlotList = [];
let cookManufacturingRouteList = [];
let cookSelectedWorkcenters = new Set();
let cookSelectedSlots = new Set();
let cookSelectedManufacturingRoutes = new Set();

function updateCookWorkcenterSummary() {
    const label = document.getElementById('cookWorkcenterSelectedLabel');
    if (!label) return;
    if (cookSelectedWorkcenters.size === 0) {
        label.textContent = '未選択';
    } else if (cookSelectedWorkcenters.size === cookWorkcenterList.length && cookWorkcenterList.length > 0) {
        label.textContent = 'すべて選択';
    } else {
        label.textContent = `${cookSelectedWorkcenters.size}件選択`;
    }
}

function updateCookSlotSummary() {
    const label = document.getElementById('cookSlotSelectedLabel');
    if (!label) return;
    if (cookSelectedSlots.size === 0) {
        label.textContent = '未選択';
    } else if (cookSelectedSlots.size === cookSlotList.length && cookSlotList.length > 0) {
        label.textContent = 'すべて選択';
    } else {
        label.textContent = `${cookSelectedSlots.size}件選択`;
    }
}

function updateCookManufacturingRouteSummary() {
    const label = document.getElementById('cookManufacturingRouteSelectedLabel');
    if (!label) return;
    const needDate = document.getElementById('cookNeedDate')?.value;
    if (!needDate) {
        label.textContent = '納期を選択してください';
        return;
    }
    if (cookManufacturingRouteList.length === 0) {
        label.textContent = '該当する製造便がありません';
        return;
    }
    if (cookSelectedManufacturingRoutes.size === 0) {
        label.textContent = '未選択';
    } else if (cookSelectedManufacturingRoutes.size === cookManufacturingRouteList.length) {
        label.textContent = 'すべて選択';
    } else {
        label.textContent = `${cookSelectedManufacturingRoutes.size}件選択`;
    }
}

function buildCookWorkcenterPanel() {
    const container = document.getElementById('cookWorkcenterOptions');
    if (!container) return;
    container.innerHTML = '';
    cookWorkcenterList.forEach(w => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = String(w.id);
        if (cookSelectedWorkcenters.has(cb.value)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) cookSelectedWorkcenters.add(cb.value);
            else cookSelectedWorkcenters.delete(cb.value);
            updateCookWorkcenterSummary();
        });
        const text = document.createElement('span');
        text.textContent = w.name || '';
        label.appendChild(cb);
        label.appendChild(text);
        container.appendChild(label);
    });
    updateCookWorkcenterSummary();
}

function buildCookSlotPanel() {
    const container = document.getElementById('cookSlotOptions');
    if (!container) return;
    container.innerHTML = '';
    cookSlotList.forEach(s => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = s.code || '';
        if (cookSelectedSlots.has(cb.value)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) cookSelectedSlots.add(cb.value);
            else cookSelectedSlots.delete(cb.value);
            updateCookSlotSummary();
        });
        const text = document.createElement('span');
        text.textContent = s.name || s.code || '';
        label.appendChild(cb);
        label.appendChild(text);
        container.appendChild(label);
    });
    updateCookSlotSummary();
}

function buildCookManufacturingRoutePanel() {
    const container = document.getElementById('cookManufacturingRouteOptions');
    if (!container) return;
    container.innerHTML = '';
    cookManufacturingRouteList.forEach(s => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = s.code || '';
        if (cookSelectedManufacturingRoutes.has(cb.value)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) cookSelectedManufacturingRoutes.add(cb.value);
            else cookSelectedManufacturingRoutes.delete(cb.value);
            updateCookManufacturingRouteSummary();
        });
        const text = document.createElement('span');
        text.textContent = s.name || s.code || '';
        label.appendChild(cb);
        label.appendChild(text);
        container.appendChild(label);
    });
    updateCookManufacturingRouteSummary();
}

async function refreshCookManufacturingRoutesFromNeedDate() {
    const needDateInput = document.getElementById('cookNeedDate');
    const needDate = needDateInput?.value || '';
    cookSelectedManufacturingRoutes = new Set();
    cookManufacturingRouteList = [];
    const container = document.getElementById('cookManufacturingRouteOptions');
    if (container) container.innerHTML = '';

    if (!needDate) {
        updateCookManufacturingRouteSummary();
        return;
    }

    try {
        cookManufacturingRouteList = await fetchCookingManufacturingRoutes(needDate) || [];
        buildCookManufacturingRoutePanel();
    } catch (e) {
        console.error('調理指示書 製造便取得エラー:', e);
        cookManufacturingRouteList = [];
        updateCookManufacturingRouteSummary();
    }
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
            <td>${row.itemName || '-'}</td>
            <td>${dateDisplay || '-'}</td>
            <td>${row.slotDisplay || '-'}</td>
        `;
        tr.style.cursor = 'pointer';
        tr.addEventListener('click', (e) => {
            if (e.target.classList.contains('cook-item-checkbox')) return;
            const cb = tr.querySelector('.cook-item-checkbox');
            if (cb) cb.checked = !cb.checked;
        });
    });

    section.style.display = 'block';
    printSection.style.display = 'flex';
    const headerCheckbox = document.getElementById('cookHeaderCheckbox');
    if (headerCheckbox) headerCheckbox.checked = false;
}

export function getSelectedCookingOrderTableIds() {
    const checked = document.querySelectorAll('.cook-item-checkbox:checked');
    const ids = [];
    checked.forEach(cb => {
        const index = Number(cb.dataset.index);
        const row = cookRows[index];
        if (row && typeof row.orderTableId === 'number') {
            ids.push(row.orderTableId);
        }
    });
    return ids;
}

function getSelectedWorkcenterIds() {
    return Array.from(cookSelectedWorkcenters.values()).map(v => Number(v));
}

function getSelectedSlotCodes() {
    return Array.from(cookSelectedSlots.values()).filter(c => c && String(c).trim());
}

function getSelectedManufacturingRouteCodes() {
    return Array.from(cookSelectedManufacturingRoutes.values()).filter(c => c && String(c).trim());
}

document.addEventListener('DOMContentLoaded', () => {
    const searchBtn = document.getElementById('cookSearchBtn');
    const headerCheckbox = document.getElementById('cookHeaderCheckbox');
    const selectAllBtn = document.getElementById('cookSelectAllBtn');
    const deselectAllBtn = document.getElementById('cookDeselectAllBtn');
    const workcenterDisplay = document.getElementById('cookWorkcenterDisplay');
    const slotDisplay = document.getElementById('cookSlotDisplay');
    const manufacturingRouteDisplay = document.getElementById('cookManufacturingRouteDisplay');
    const needDateInput = document.getElementById('cookNeedDate');

    if (!searchBtn) return;

    needDateInput?.addEventListener('change', () => {
        refreshCookManufacturingRoutesFromNeedDate();
    });

    (async () => {
        try {
            const [wcs, slots] = await Promise.all([
                fetchCookingWorkcenters(),
                fetchCookingSlots()
            ]);
            cookWorkcenterList = wcs || [];
            cookSlotList = slots || [];
            buildCookWorkcenterPanel();
            buildCookSlotPanel();
        } catch (e) {
            console.error('調理指示書 マスタ取得エラー:', e);
        }
    })();

    function closeAllCookPanels() {
        const w = document.getElementById('cookWorkcenterOptions');
        const s = document.getElementById('cookSlotOptions');
        const m = document.getElementById('cookManufacturingRouteOptions');
        if (w) w.style.display = 'none';
        if (s) s.style.display = 'none';
        if (m) m.style.display = 'none';
    }

    if (workcenterDisplay) {
        workcenterDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('cookWorkcenterOptions');
            if (!panel) return;
            const isHidden = panel.style.display === 'none' || panel.style.display === '';
            closeAllCookPanels();
            panel.style.display = isHidden ? 'block' : 'none';
        });
    }

    if (slotDisplay) {
        slotDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('cookSlotOptions');
            if (!panel) return;
            const isHidden = panel.style.display === 'none' || panel.style.display === '';
            closeAllCookPanels();
            panel.style.display = isHidden ? 'block' : 'none';
        });
    }

    if (manufacturingRouteDisplay) {
        manufacturingRouteDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('cookManufacturingRouteOptions');
            if (!panel) return;
            const nd = document.getElementById('cookNeedDate')?.value;
            if (!nd) {
                alert('先に納期を選択してください');
                return;
            }
            const isHidden = panel.style.display === 'none' || panel.style.display === '';
            closeAllCookPanels();
            panel.style.display = isHidden ? 'block' : 'none';
        });
    }

    document.addEventListener('click', (e) => {
        const dropdown = (e.target instanceof HTMLElement)
            ? e.target.closest('#screen-cooking-instruction .multi-select-dropdown')
            : null;
        if (!dropdown) {
            closeAllCookPanels();
        }
    });

    searchBtn.addEventListener('click', async () => {
        const needDate = document.getElementById('cookNeedDate').value;

        if (!needDate) {
            alert('納期を入力してください');
            return;
        }

        const workcenterIds = getSelectedWorkcenterIds();
        const slotCodes = getSelectedSlotCodes();
        const manufacturingRouteCodes = getSelectedManufacturingRouteCodes();

        try {
            const res = await searchCookingInstruction(needDate, workcenterIds, slotCodes, manufacturingRouteCodes);
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
