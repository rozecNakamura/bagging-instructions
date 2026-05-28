import {
    searchProductionInstruction,
    fetchProductionInstructionWorkcenters,
    fetchProductionInstructionSlots
} from './api.js';
import { productionInstructionMultiSelectLabel } from './production_instruction_common.js';

let prodRows = [];
let aggregatedProdRows = [];
let prodSlotList = [];
let prodSelectedSlotCodes = new Set();

function formatQtySum(sum) {
    if (sum === 0) return '0';
    const rounded = Math.round(sum * 1000) / 1000;
    if (Number.isInteger(rounded)) return String(rounded);
    return rounded.toFixed(3).replace(/0+$/, '').replace(/\.$/, '');
}

function buildAggregatedRows(sortedRows) {
    const map = new Map();
    sortedRows.forEach((row, index) => {
        const key = `${row.itemCode ?? ''}|${row.slotDisplay ?? ''}`;
        if (!map.has(key)) {
            map.set(key, {
                itemCode: row.itemCode,
                itemName: row.itemName,
                needDate: row.needDate,
                slotDisplay: row.slotDisplay,
                unitName: row.unitName,
                sumQty: parseFloat(row.quantityDisplay) || 0,
                indices: [index]
            });
        } else {
            const entry = map.get(key);
            entry.sumQty += parseFloat(row.quantityDisplay) || 0;
            entry.indices.push(index);
        }
    });
    return Array.from(map.values()).map(entry => ({
        itemCode: entry.itemCode,
        itemName: entry.itemName,
        needDate: entry.needDate,
        slotDisplay: entry.slotDisplay,
        unitName: entry.unitName,
        quantityDisplay: formatQtySum(entry.sumQty),
        indices: entry.indices
    }));
}

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

    // 製造便でグルーピングするためにソート（便→品目名→ID順）
    const sorted = [...rows].sort((a, b) => {
        const sc = (a.slotDisplay || '').localeCompare(b.slotDisplay || '', 'ja');
        if (sc !== 0) return sc;
        return (a.itemName || '').localeCompare(b.itemName || '', 'ja');
    });
    prodRows = sorted;
    aggregatedProdRows = buildAggregatedRows(sorted);

    countEl.textContent = `${aggregatedProdRows.length}件`;
    tbody.innerHTML = '';

    let currentSlot = undefined;
    aggregatedProdRows.forEach((row, aggIndex) => {
        const slot = row.slotDisplay || '';

        // 製造便が変わるたびにグループヘッダー行を挿入
        if (slot !== currentSlot) {
            currentSlot = slot;
            const groupTr = document.createElement('tr');
            groupTr.style.cssText = 'background:#e8eaf6; font-weight:bold; pointer-events:none;';
            const td = document.createElement('td');
            td.colSpan = 7;
            td.textContent = `【${slot || '便なし'}】`;
            groupTr.appendChild(td);
            tbody.appendChild(groupTr);
        }

        const tr = tbody.insertRow();
        const dateDisplay = row.needDate || '-';
        tr.innerHTML = `
            <td><input type="checkbox" class="prod-item-checkbox" data-index="${aggIndex}"></td>
            <td>${row.itemCode || '-'}</td>
            <td>${row.itemName || '-'}</td>
            <td>${row.quantityDisplay || '-'}</td>
            <td>${row.unitName || '-'}</td>
            <td>${dateDisplay || '-'}</td>
            <td>${slot || '-'}</td>
        `;
        tr.style.cursor = 'pointer';
        tr.addEventListener('click', (e) => {
            if (e.target.classList.contains('prod-item-checkbox')) return;
            const cb = tr.querySelector('.prod-item-checkbox');
            if (cb) cb.checked = !cb.checked;
        });
    });

    section.style.display = 'block';
    printSection.style.display = 'flex';
    const headerCheckbox = document.getElementById('prodHeaderCheckbox');
    tbody.querySelectorAll('.prod-item-checkbox').forEach(cb => { cb.checked = true; });
    if (headerCheckbox) headerCheckbox.checked = true;
}

export function getSelectedOrderIds() {
    const checked = document.querySelectorAll('.prod-item-checkbox:checked');
    const ids = [];
    checked.forEach(cb => {
        const aggIndex = Number(cb.dataset.index);
        const aggRow = aggregatedProdRows[aggIndex];
        if (aggRow) {
            aggRow.indices.forEach(rawIndex => {
                const row = prodRows[rawIndex];
                if (row && typeof row.orderTableId === 'number') {
                    ids.push(row.orderTableId);
                }
            });
        }
    });
    return ids;
}

/** @returns {{ needDate: string, workcenterIds: number[], slotCodes: string[] }} */
export function getProductionInstructionReportFilter() {
    const needDate = document.getElementById('prodNeedDate')?.value || '';
    const wcVal = document.getElementById('prodWorkcenter')?.value || '';
    const wcId = wcVal ? Number(wcVal) : 0;
    return {
        needDate,
        workcenterIds: wcId > 0 ? [wcId] : [],
        slotCodes: Array.from(prodSelectedSlotCodes).filter(s => s)
    };
}

function updateProdSlotLabel() {
    const slotLabel = document.getElementById('prodSlotSelectedLabel');
    if (slotLabel) {
        slotLabel.textContent = productionInstructionMultiSelectLabel(
            prodSelectedSlotCodes.size,
            prodSlotList.length
        );
    }
}

function buildProdSlotPanel() {
    const slotContainer = document.getElementById('prodSlotOptions');
    if (!slotContainer) return;

    slotContainer.innerHTML = '';

    prodSlotList.forEach(s => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = s.code || '';
        if (prodSelectedSlotCodes.has(s.code)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) {
                if (s.code) prodSelectedSlotCodes.add(s.code);
            } else {
                prodSelectedSlotCodes.delete(s.code);
            }
            updateProdSlotLabel();
        });
        const text = document.createElement('span');
        text.textContent = s.name || s.code || '';
        label.appendChild(cb);
        label.appendChild(text);
        slotContainer.appendChild(label);
    });

    updateProdSlotLabel();
}

function populateProdWorkcenterSelect(list) {
    const sel = document.getElementById('prodWorkcenter');
    if (!sel) return;
    while (sel.options.length > 1) sel.remove(1);
    (list || []).forEach(w => {
        const opt = document.createElement('option');
        opt.value = String(w.id);
        opt.textContent = w.name || String(w.id);
        sel.appendChild(opt);
    });
}

document.addEventListener('DOMContentLoaded', () => {
    const searchBtn = document.getElementById('prodSearchBtn');
    const headerCheckbox = document.getElementById('prodHeaderCheckbox');
    const selectAllBtn = document.getElementById('prodSelectAllBtn');
    const deselectAllBtn = document.getElementById('prodDeselectAllBtn');
    const slotDisplay = document.getElementById('prodSlotDisplay');

    if (!searchBtn) return;

    (async () => {
        try {
            const wcs = await fetchProductionInstructionWorkcenters();
            populateProdWorkcenterSelect(wcs || []);
        } catch (e) {
            console.error('調味液配合表仕様 マスタ取得エラー:', e);
        }
    })();

    async function loadProdSlots(needDate) {
        prodSelectedSlotCodes = new Set();
        prodSlotList = [];
        const container = document.getElementById('prodSlotOptions');
        if (container) container.innerHTML = '';
        if (!needDate) {
            updateProdSlotLabel();
            return;
        }
        try {
            prodSlotList = await fetchProductionInstructionSlots(needDate) || [];
            buildProdSlotPanel();
        } catch (e) {
            console.error('調味液配合表仕様 便一覧取得エラー:', e);
            updateProdSlotLabel();
        }
    }

    const prodNeedDateInput = document.getElementById('prodNeedDate');
    const onProdNeedDateChanged = () => loadProdSlots(prodNeedDateInput?.value || '');
    prodNeedDateInput?.addEventListener('change', onProdNeedDateChanged);
    prodNeedDateInput?.addEventListener('input', onProdNeedDateChanged);
    if (prodNeedDateInput?.value) {
        loadProdSlots(prodNeedDateInput.value);
    }

    function closeAllProdPanels() {
        const p2 = document.getElementById('prodSlotOptions');
        if (p2) p2.style.display = 'none';
    }

    if (slotDisplay) {
        slotDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('prodSlotOptions');
            if (!panel) return;
            const isHidden = panel.style.display === 'none' || panel.style.display === '';
            closeAllProdPanels();
            panel.style.display = isHidden ? 'block' : 'none';
        });
    }

    document.addEventListener('click', (e) => {
        const dropdown = (e.target instanceof HTMLElement)
            ? e.target.closest('#screen-production-instruction .multi-select-dropdown')
            : null;
        if (!dropdown) {
            closeAllProdPanels();
        }
    });

    searchBtn.addEventListener('click', async () => {
        const needDate = document.getElementById('prodNeedDate').value;
        const wcVal = document.getElementById('prodWorkcenter')?.value || '';
        const workcenterIds = wcVal ? [Number(wcVal)] : [];
        const slotCodes = Array.from(prodSelectedSlotCodes);

        if (!needDate) {
            alert('納期を入力してください');
            return;
        }

        try {
            const res = await searchProductionInstruction(needDate, workcenterIds, slotCodes);
            displayProductionResults(res.rows || []);
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
