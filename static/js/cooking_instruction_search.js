import { searchCookingInstruction, fetchCookingWorkcenters, fetchCookingSlots, fetchCookingClassification3s, exportCookingInstructionExcel } from './api.js';

let cookRows = [];
let cookWorkcenterList = [];
let cookSlotList = [];
let cookClassification3List = [];
let cookSelectedSlots = new Set();
let cookSelectedClassification3s = new Set();

async function loadCookWorkcenters() {
    const sel = document.getElementById('cookWorkcenter');
    if (!sel) return;
    try {
        cookWorkcenterList = await fetchCookingWorkcenters() || [];
        sel.innerHTML = '';
        const empty = document.createElement('option');
        empty.value = '';
        empty.textContent = 'すべて';
        sel.appendChild(empty);
        for (const w of cookWorkcenterList) {
            const opt = document.createElement('option');
            opt.value = String(w.id);
            opt.textContent = w.code ? `${w.code} ${w.name}` : (w.name || '');
            sel.appendChild(opt);
        }
        const defaultWc = cookWorkcenterList.find(w => w.code === '11011');
        if (defaultWc) sel.value = String(defaultWc.id);
    } catch (e) {
        console.error('調理指示書 作業区取得エラー:', e);
        if (sel) sel.innerHTML = '<option value="">取得に失敗しました</option>';
    }
}

function updateCookSlotSummary() {
    const label = document.getElementById('cookSlotSelectedLabel');
    if (!label) return;
    if (cookSlotList.length === 0) {
        const needDate = document.getElementById('cookNeedDate')?.value;
        label.textContent = needDate ? '該当する便がありません' : '納期を選択してください';
        return;
    }
    if (cookSelectedSlots.size === 0) {
        label.textContent = '未選択';
    } else if (cookSelectedSlots.size === cookSlotList.length && cookSlotList.length > 0) {
        label.textContent = 'すべて選択';
    } else {
        label.textContent = `${cookSelectedSlots.size}件選択`;
    }
}

function updateCookClassification3Summary() {
    const label = document.getElementById('cookClassification3SelectedLabel');
    if (!label) return;
    if (cookClassification3List.length === 0) {
        label.textContent = '未選択';
        return;
    }
    if (cookSelectedClassification3s.size === 0) {
        label.textContent = '未選択';
    } else if (cookSelectedClassification3s.size === cookClassification3List.length && cookClassification3List.length > 0) {
        label.textContent = 'すべて選択';
    } else {
        label.textContent = `${cookSelectedClassification3s.size}件選択`;
    }
}

function buildCookClassification3Panel() {
    const container = document.getElementById('cookClassification3Options');
    if (!container) return;
    container.innerHTML = '';
    cookClassification3List.forEach(c => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = c.code || '';
        if (cookSelectedClassification3s.has(cb.value)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) cookSelectedClassification3s.add(cb.value);
            else cookSelectedClassification3s.delete(cb.value);
            updateCookClassification3Summary();
        });
        const text = document.createElement('span');
        text.textContent = c.code ? `${c.code} ${c.name}` : (c.name || '');
        label.appendChild(cb);
        label.appendChild(text);
        container.appendChild(label);
    });
    updateCookClassification3Summary();
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

async function loadCookSlots(needDate) {
    cookSelectedSlots = new Set();
    cookSlotList = [];
    const container = document.getElementById('cookSlotOptions');
    if (container) container.innerHTML = '';

    if (!needDate) {
        updateCookSlotSummary();
        return;
    }

    try {
        cookSlotList = await fetchCookingSlots(needDate) || [];
        buildCookSlotPanel();
    } catch (e) {
        console.error('調理指示書 便取得エラー:', e);
        cookSlotList = [];
        updateCookSlotSummary();
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
            <td><input type="checkbox" class="cook-item-checkbox" data-index="${index}" checked></td>
            <td>${row.itemCode || '-'}</td>
            <td>${row.itemName || '-'}</td>
            <td>${row.quantityDisplay || '-'}</td>
            <td>${row.unitName || '-'}</td>
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
    if (headerCheckbox) headerCheckbox.checked = true;
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
    const sel = document.getElementById('cookWorkcenter');
    const v = sel?.value;
    if (!v) return [];
    const id = Number(v);
    return Number.isFinite(id) && id > 0 ? [id] : [];
}

function getSelectedSlotCodes() {
    return Array.from(cookSelectedSlots.values()).filter(c => c && String(c).trim());
}

function getSelectedClassification3Codes() {
    return Array.from(cookSelectedClassification3s.values()).filter(c => c && String(c).trim());
}

document.addEventListener('DOMContentLoaded', () => {
    const searchBtn = document.getElementById('cookSearchBtn');
    const headerCheckbox = document.getElementById('cookHeaderCheckbox');
    const selectAllBtn = document.getElementById('cookSelectAllBtn');
    const deselectAllBtn = document.getElementById('cookDeselectAllBtn');
    const slotDisplay = document.getElementById('cookSlotDisplay');
    const classification3Display = document.getElementById('cookClassification3Display');
    const needDateInput = document.getElementById('cookNeedDate');

    if (!searchBtn) return;

    needDateInput?.addEventListener('change', () => {
        loadCookSlots(needDateInput.value);
    });

    loadCookWorkcenters();

    (async () => {
        try {
            const c3s = await fetchCookingClassification3s();
            cookClassification3List = c3s || [];
            buildCookClassification3Panel();
        } catch (e) {
            console.error('調理指示書 作業名マスタ取得エラー:', e);
        }
    })();

    function closeAllCookPanels() {
        const s = document.getElementById('cookSlotOptions');
        const c = document.getElementById('cookClassification3Options');
        if (s) s.style.display = 'none';
        if (c) c.style.display = 'none';
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

    if (classification3Display) {
        classification3Display.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('cookClassification3Options');
            if (!panel) return;
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
        const classification3Codes = getSelectedClassification3Codes();

        try {
            const res = await searchCookingInstruction(needDate, workcenterIds, slotCodes, classification3Codes);
            cookRows = res.rows || [];
            displayCookingResults(cookRows);
        } catch (e) {
            alert('検索に失敗しました: ' + e.message);
            console.error(e);
        }
    });

    document.getElementById('cookExcelBtn')?.addEventListener('click', async () => {
        const needDate = document.getElementById('cookNeedDate').value;
        if (!needDate) {
            alert('納期を入力してください');
            return;
        }
        try {
            const blob = await exportCookingInstructionExcel(
                needDate,
                getSelectedWorkcenterIds(),
                getSelectedSlotCodes(),
                getSelectedClassification3Codes()
            );
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = '調理指示書.xlsx';
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
        } catch (e) {
            alert('Excel出力に失敗しました: ' + e.message);
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
