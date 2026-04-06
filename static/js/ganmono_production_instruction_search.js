import {
    searchProductionInstruction,
    fetchProductionInstructionWorkcenters,
    fetchProductionInstructionSlots
} from './api.js';
import { productionInstructionMultiSelectLabel } from './production_instruction_common.js';

let gmtRows = [];
let gmtWorkcenterList = [];
let gmtSlotList = [];
let gmtSelectedWorkcenterIds = new Set();
let gmtSelectedSlotCodes = new Set();

const screenSel = '#screen-production-instruction-ganmono';

function displayGmtResults(rows) {
    const section = document.getElementById('gmtResultsSection');
    const printSection = document.getElementById('gmtPrintSection');
    const countEl = document.getElementById('gmtResultCount');
    const tbody = document.getElementById('gmtResultsBody');

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
            <td><input type="checkbox" class="gmt-item-checkbox" data-index="${index}"></td>
            <td>${row.itemName || '-'}</td>
            <td>${dateDisplay || '-'}</td>
            <td>${row.slotDisplay || '-'}</td>
        `;
        tr.style.cursor = 'pointer';
        tr.addEventListener('click', (e) => {
            if (e.target.classList.contains('gmt-item-checkbox')) return;
            const cb = tr.querySelector('.gmt-item-checkbox');
            if (cb) cb.checked = !cb.checked;
        });
    });

    section.style.display = 'block';
    printSection.style.display = 'block';
    const headerCheckbox = document.getElementById('gmtHeaderCheckbox');
    if (headerCheckbox) headerCheckbox.checked = false;
}

export function getGmtSelectedOrderIds() {
    const checked = document.querySelectorAll(`${screenSel} .gmt-item-checkbox:checked`);
    const ids = [];
    checked.forEach(cb => {
        const index = Number(cb.dataset.index);
        const row = gmtRows[index];
        if (row && typeof row.orderTableId === 'number') {
            ids.push(row.orderTableId);
        }
    });
    return ids;
}

/** @returns {{ needDate: string, workcenterIds: number[], slotCodes: string[] }} */
export function getGmtReportFilter() {
    const needDate = document.getElementById('gmtNeedDate')?.value || '';
    return {
        needDate,
        workcenterIds: Array.from(gmtSelectedWorkcenterIds).map(id => Number(id)).filter(id => id > 0),
        slotCodes: Array.from(gmtSelectedSlotCodes).filter(s => s)
    };
}

function updateGmtWorkcenterSlotLabels() {
    const wcLabel = document.getElementById('gmtWorkcenterSelectedLabel');
    const slotLabel = document.getElementById('gmtSlotSelectedLabel');

    if (wcLabel) {
        wcLabel.textContent = productionInstructionMultiSelectLabel(
            gmtSelectedWorkcenterIds.size,
            gmtWorkcenterList.length
        );
    }

    if (slotLabel) {
        slotLabel.textContent = productionInstructionMultiSelectLabel(
            gmtSelectedSlotCodes.size,
            gmtSlotList.length
        );
    }
}

function buildGmtWorkcenterSlotPanels() {
    const wcContainer = document.getElementById('gmtWorkcenterOptions');
    const slotContainer = document.getElementById('gmtSlotOptions');
    if (!wcContainer || !slotContainer) return;

    wcContainer.innerHTML = '';
    slotContainer.innerHTML = '';

    gmtWorkcenterList.forEach(w => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = String(w.id);
        if (gmtSelectedWorkcenterIds.has(w.id)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) {
                gmtSelectedWorkcenterIds.add(w.id);
            } else {
                gmtSelectedWorkcenterIds.delete(w.id);
            }
            updateGmtWorkcenterSlotLabels();
        });
        const text = document.createElement('span');
        text.textContent = w.name || '';
        label.appendChild(cb);
        label.appendChild(text);
        wcContainer.appendChild(label);
    });

    gmtSlotList.forEach(s => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = s.code || '';
        if (gmtSelectedSlotCodes.has(s.code)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) {
                if (s.code) gmtSelectedSlotCodes.add(s.code);
            } else {
                gmtSelectedSlotCodes.delete(s.code);
            }
            updateGmtWorkcenterSlotLabels();
        });
        const text = document.createElement('span');
        text.textContent = s.name || s.code || '';
        label.appendChild(cb);
        label.appendChild(text);
        slotContainer.appendChild(label);
    });

    updateGmtWorkcenterSlotLabels();
}

document.addEventListener('DOMContentLoaded', () => {
    const searchBtn = document.getElementById('gmtSearchBtn');
    const headerCheckbox = document.getElementById('gmtHeaderCheckbox');
    const selectAllBtn = document.getElementById('gmtSelectAllBtn');
    const deselectAllBtn = document.getElementById('gmtDeselectAllBtn');
    const wcDisplay = document.getElementById('gmtWorkcenterDisplay');
    const slotDisplay = document.getElementById('gmtSlotDisplay');

    if (!searchBtn) return;

    (async () => {
        try {
            const [wcs, slots] = await Promise.all([
                fetchProductionInstructionWorkcenters(),
                fetchProductionInstructionSlots()
            ]);
            gmtWorkcenterList = wcs || [];
            gmtSlotList = slots || [];
            buildGmtWorkcenterSlotPanels();
        } catch (e) {
            console.error('生産指示書_がんもの炊き合わせ マスタ取得エラー:', e);
        }
    })();

    function closeAllGmtPanels() {
        const p1 = document.getElementById('gmtWorkcenterOptions');
        const p2 = document.getElementById('gmtSlotOptions');
        if (p1) p1.style.display = 'none';
        if (p2) p2.style.display = 'none';
    }

    if (wcDisplay) {
        wcDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('gmtWorkcenterOptions');
            if (!panel) return;
            const isHidden = panel.style.display === 'none' || panel.style.display === '';
            closeAllGmtPanels();
            panel.style.display = isHidden ? 'block' : 'none';
        });
    }

    if (slotDisplay) {
        slotDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('gmtSlotOptions');
            if (!panel) return;
            const isHidden = panel.style.display === 'none' || panel.style.display === '';
            closeAllGmtPanels();
            panel.style.display = isHidden ? 'block' : 'none';
        });
    }

    document.addEventListener('click', (e) => {
        const dropdown = (e.target instanceof HTMLElement)
            ? e.target.closest(`${screenSel} .multi-select-dropdown`)
            : null;
        if (!dropdown) {
            closeAllGmtPanels();
        }
    });

    searchBtn.addEventListener('click', async () => {
        const needDate = document.getElementById('gmtNeedDate').value;
        const workcenterIds = Array.from(gmtSelectedWorkcenterIds);
        const slotCodes = Array.from(gmtSelectedSlotCodes);

        if (!needDate) {
            alert('納期を入力してください');
            return;
        }

        try {
            const res = await searchProductionInstruction(needDate, workcenterIds, slotCodes);
            gmtRows = res.rows || [];
            displayGmtResults(gmtRows);
        } catch (e) {
            alert('検索に失敗しました: ' + e.message);
            console.error(e);
        }
    });

    headerCheckbox?.addEventListener('change', (e) => {
        const checked = e.target.checked;
        document.querySelectorAll(`${screenSel} .gmt-item-checkbox`).forEach(cb => {
            cb.checked = checked;
        });
    });

    selectAllBtn?.addEventListener('click', () => {
        document.querySelectorAll(`${screenSel} .gmt-item-checkbox`).forEach(cb => { cb.checked = true; });
        if (headerCheckbox) headerCheckbox.checked = true;
    });

    deselectAllBtn?.addEventListener('click', () => {
        document.querySelectorAll(`${screenSel} .gmt-item-checkbox`).forEach(cb => { cb.checked = false; });
        if (headerCheckbox) headerCheckbox.checked = false;
    });
});
