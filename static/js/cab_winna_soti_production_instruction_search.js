import {
    searchProductionInstruction,
    fetchProductionInstructionWorkcenters,
    fetchProductionInstructionSlots
} from './api.js';
import { productionInstructionMultiSelectLabel } from './production_instruction_common.js';

let cwsRows = [];
let cwsWorkcenterList = [];
let cwsSlotList = [];
let cwsSelectedWorkcenterIds = new Set();
let cwsSelectedSlotCodes = new Set();

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
        const dateDisplay = row.needDate || '-';
        tr.innerHTML = `
            <td><input type="checkbox" class="cws-item-checkbox" data-index="${index}"></td>
            <td>${row.itemName || '-'}</td>
            <td>${dateDisplay || '-'}</td>
            <td>${row.slotDisplay || '-'}</td>
        `;
        tr.style.cursor = 'pointer';
        tr.addEventListener('click', (e) => {
            if (e.target.classList.contains('cws-item-checkbox')) return;
            const cb = tr.querySelector('.cws-item-checkbox');
            if (cb) cb.checked = !cb.checked;
        });
    });

    section.style.display = 'block';
    printSection.style.display = 'block';
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
        workcenterIds: Array.from(cwsSelectedWorkcenterIds).map(id => Number(id)).filter(id => id > 0),
        slotCodes: Array.from(cwsSelectedSlotCodes).filter(s => s)
    };
}

function updateCwsWorkcenterSlotLabels() {
    const wcLabel = document.getElementById('cwsWorkcenterSelectedLabel');
    const slotLabel = document.getElementById('cwsSlotSelectedLabel');

    if (wcLabel) {
        wcLabel.textContent = productionInstructionMultiSelectLabel(
            cwsSelectedWorkcenterIds.size,
            cwsWorkcenterList.length
        );
    }

    if (slotLabel) {
        slotLabel.textContent = productionInstructionMultiSelectLabel(
            cwsSelectedSlotCodes.size,
            cwsSlotList.length
        );
    }
}

function buildCwsWorkcenterSlotPanels() {
    const wcContainer = document.getElementById('cwsWorkcenterOptions');
    const slotContainer = document.getElementById('cwsSlotOptions');
    if (!wcContainer || !slotContainer) return;

    wcContainer.innerHTML = '';
    slotContainer.innerHTML = '';

    cwsWorkcenterList.forEach(w => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = String(w.id);
        if (cwsSelectedWorkcenterIds.has(w.id)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) {
                cwsSelectedWorkcenterIds.add(w.id);
            } else {
                cwsSelectedWorkcenterIds.delete(w.id);
            }
            updateCwsWorkcenterSlotLabels();
        });
        const text = document.createElement('span');
        text.textContent = w.name || '';
        label.appendChild(cb);
        label.appendChild(text);
        wcContainer.appendChild(label);
    });

    cwsSlotList.forEach(s => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = s.code || '';
        if (cwsSelectedSlotCodes.has(s.code)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) {
                if (s.code) cwsSelectedSlotCodes.add(s.code);
            } else {
                cwsSelectedSlotCodes.delete(s.code);
            }
            updateCwsWorkcenterSlotLabels();
        });
        const text = document.createElement('span');
        text.textContent = s.name || s.code || '';
        label.appendChild(cb);
        label.appendChild(text);
        slotContainer.appendChild(label);
    });

    updateCwsWorkcenterSlotLabels();
}

document.addEventListener('DOMContentLoaded', () => {
    const searchBtn = document.getElementById('cwsSearchBtn');
    const headerCheckbox = document.getElementById('cwsHeaderCheckbox');
    const selectAllBtn = document.getElementById('cwsSelectAllBtn');
    const deselectAllBtn = document.getElementById('cwsDeselectAllBtn');
    const wcDisplay = document.getElementById('cwsWorkcenterDisplay');
    const slotDisplay = document.getElementById('cwsSlotDisplay');

    if (!searchBtn) return;

    (async () => {
        try {
            const [wcs, slots] = await Promise.all([
                fetchProductionInstructionWorkcenters(),
                fetchProductionInstructionSlots()
            ]);
            cwsWorkcenterList = wcs || [];
            cwsSlotList = slots || [];
            buildCwsWorkcenterSlotPanels();
        } catch (e) {
            console.error('生産指示書_キャベツとウィンナーのソティ マスタ取得エラー:', e);
        }
    })();

    function closeAllCwsPanels() {
        const p1 = document.getElementById('cwsWorkcenterOptions');
        const p2 = document.getElementById('cwsSlotOptions');
        if (p1) p1.style.display = 'none';
        if (p2) p2.style.display = 'none';
    }

    if (wcDisplay) {
        wcDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('cwsWorkcenterOptions');
            if (!panel) return;
            const isHidden = panel.style.display === 'none' || panel.style.display === '';
            closeAllCwsPanels();
            panel.style.display = isHidden ? 'block' : 'none';
        });
    }

    if (slotDisplay) {
        slotDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('cwsSlotOptions');
            if (!panel) return;
            const isHidden = panel.style.display === 'none' || panel.style.display === '';
            closeAllCwsPanels();
            panel.style.display = isHidden ? 'block' : 'none';
        });
    }

    document.addEventListener('click', (e) => {
        const dropdown = (e.target instanceof HTMLElement)
            ? e.target.closest(`${screenSel} .multi-select-dropdown`)
            : null;
        if (!dropdown) {
            closeAllCwsPanels();
        }
    });

    searchBtn.addEventListener('click', async () => {
        const needDate = document.getElementById('cwsNeedDate').value;
        const workcenterIds = Array.from(cwsSelectedWorkcenterIds);
        const slotCodes = Array.from(cwsSelectedSlotCodes);

        if (!needDate) {
            alert('納期を入力してください');
            return;
        }

        try {
            const res = await searchProductionInstruction(needDate, workcenterIds, slotCodes);
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
