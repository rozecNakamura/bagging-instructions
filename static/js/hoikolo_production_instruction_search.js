import {
    searchProductionInstruction,
    fetchProductionInstructionWorkcenters,
    fetchProductionInstructionSlots
} from './api.js';
import { productionInstructionMultiSelectLabel } from './production_instruction_common.js';

let hclRows = [];
let hclWorkcenterList = [];
let hclSlotList = [];
let hclSelectedWorkcenterIds = new Set();
let hclSelectedSlotCodes = new Set();

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
        const dateDisplay = row.needDate || '-';
        tr.innerHTML = `
            <td><input type="checkbox" class="hcl-item-checkbox" data-index="${index}"></td>
            <td>${row.itemName || '-'}</td>
            <td>${dateDisplay || '-'}</td>
            <td>${row.slotDisplay || '-'}</td>
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
        workcenterIds: Array.from(hclSelectedWorkcenterIds).map(id => Number(id)).filter(id => id > 0),
        slotCodes: Array.from(hclSelectedSlotCodes).filter(s => s)
    };
}

function updateHclWorkcenterSlotLabels() {
    const wcLabel = document.getElementById('hclWorkcenterSelectedLabel');
    const slotLabel = document.getElementById('hclSlotSelectedLabel');

    if (wcLabel) {
        wcLabel.textContent = productionInstructionMultiSelectLabel(
            hclSelectedWorkcenterIds.size,
            hclWorkcenterList.length
        );
    }

    if (slotLabel) {
        slotLabel.textContent = productionInstructionMultiSelectLabel(
            hclSelectedSlotCodes.size,
            hclSlotList.length
        );
    }
}

function buildHclWorkcenterSlotPanels() {
    const wcContainer = document.getElementById('hclWorkcenterOptions');
    const slotContainer = document.getElementById('hclSlotOptions');
    if (!wcContainer || !slotContainer) return;

    wcContainer.innerHTML = '';
    slotContainer.innerHTML = '';

    hclWorkcenterList.forEach(w => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = String(w.id);
        if (hclSelectedWorkcenterIds.has(w.id)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) {
                hclSelectedWorkcenterIds.add(w.id);
            } else {
                hclSelectedWorkcenterIds.delete(w.id);
            }
            updateHclWorkcenterSlotLabels();
        });
        const text = document.createElement('span');
        text.textContent = w.name || '';
        label.appendChild(cb);
        label.appendChild(text);
        wcContainer.appendChild(label);
    });

    hclSlotList.forEach(s => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = s.code || '';
        if (hclSelectedSlotCodes.has(s.code)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) {
                if (s.code) hclSelectedSlotCodes.add(s.code);
            } else {
                hclSelectedSlotCodes.delete(s.code);
            }
            updateHclWorkcenterSlotLabels();
        });
        const text = document.createElement('span');
        text.textContent = s.name || s.code || '';
        label.appendChild(cb);
        label.appendChild(text);
        slotContainer.appendChild(label);
    });

    updateHclWorkcenterSlotLabels();
}

document.addEventListener('DOMContentLoaded', () => {
    const searchBtn = document.getElementById('hclSearchBtn');
    const headerCheckbox = document.getElementById('hclHeaderCheckbox');
    const selectAllBtn = document.getElementById('hclSelectAllBtn');
    const deselectAllBtn = document.getElementById('hclDeselectAllBtn');
    const wcDisplay = document.getElementById('hclWorkcenterDisplay');
    const slotDisplay = document.getElementById('hclSlotDisplay');

    if (!searchBtn) return;

    (async () => {
        try {
            const [wcs, slots] = await Promise.all([
                fetchProductionInstructionWorkcenters(),
                fetchProductionInstructionSlots()
            ]);
            hclWorkcenterList = wcs || [];
            hclSlotList = slots || [];
            buildHclWorkcenterSlotPanels();
        } catch (e) {
            console.error('生産指示書_ホイコーロー マスタ取得エラー:', e);
        }
    })();

    function closeAllHclPanels() {
        const p1 = document.getElementById('hclWorkcenterOptions');
        const p2 = document.getElementById('hclSlotOptions');
        if (p1) p1.style.display = 'none';
        if (p2) p2.style.display = 'none';
    }

    if (wcDisplay) {
        wcDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('hclWorkcenterOptions');
            if (!panel) return;
            const isHidden = panel.style.display === 'none' || panel.style.display === '';
            closeAllHclPanels();
            panel.style.display = isHidden ? 'block' : 'none';
        });
    }

    if (slotDisplay) {
        slotDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('hclSlotOptions');
            if (!panel) return;
            const isHidden = panel.style.display === 'none' || panel.style.display === '';
            closeAllHclPanels();
            panel.style.display = isHidden ? 'block' : 'none';
        });
    }

    document.addEventListener('click', (e) => {
        const dropdown = (e.target instanceof HTMLElement)
            ? e.target.closest(`${screenSel} .multi-select-dropdown`)
            : null;
        if (!dropdown) {
            closeAllHclPanels();
        }
    });

    searchBtn.addEventListener('click', async () => {
        const needDate = document.getElementById('hclNeedDate').value;
        const workcenterIds = Array.from(hclSelectedWorkcenterIds);
        const slotCodes = Array.from(hclSelectedSlotCodes);

        if (!needDate) {
            alert('納期を入力してください');
            return;
        }

        try {
            const res = await searchProductionInstruction(needDate, workcenterIds, slotCodes);
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
