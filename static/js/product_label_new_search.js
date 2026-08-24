/**
 * 現品票印刷（新）：マスタ読込・検索・結果表示。
 * 1行＝子品目1件（最上位完成品から BOM を再帰探索した子孫品目。孫以下も含む）。
 * マスタ系プルダウンは既存の現品票画面と同じ API を流用する。
 */
import {
    fetchMajorClassifications,
    fetchProductLabelMiddleClassifications,
    fetchProductLabelWorkcenters,
    fetchProductLabelWarehouses,
    fetchProductLabelNewSlots,
    searchProductLabelNew,
} from './api.js';

let plnRows = [];
let plnSlotList = [];
let plnSelectedSlotCodes = new Set();

function formatDateYyyymmdd(yyyymmdd) {
    if (!yyyymmdd || yyyymmdd.length !== 8) return yyyymmdd || '-';
    return `${yyyymmdd.slice(0, 4)}-${yyyymmdd.slice(4, 6)}-${yyyymmdd.slice(6, 8)}`;
}

/** マスタ一覧を1回だけ取得し、指定した複数の select に同じ選択肢を流し込む。 */
async function loadSelect(selectIds, fetcher, labelFn, errorText) {
    const ids = Array.isArray(selectIds) ? selectIds : [selectIds];
    const sels = ids.map(id => document.getElementById(id)).filter(Boolean);
    if (sels.length === 0) return;
    try {
        const list = await fetcher();
        for (const sel of sels) {
            sel.innerHTML = '';
            const empty = document.createElement('option');
            empty.value = '';
            empty.textContent = '指定なし（すべて）';
            sel.appendChild(empty);
            for (const item of list) {
                const opt = document.createElement('option');
                opt.value = String(item.id);
                opt.dataset.code = item.code || '';
                opt.textContent = labelFn(item);
                sel.appendChild(opt);
            }
        }
    } catch (e) {
        for (const sel of sels) sel.innerHTML = `<option value="">${errorText}</option>`;
        console.error(e);
    }
}

async function loadMajorClassifications() {
    await loadSelect(
        ['productLabelNewMajorClass', 'productLabelNewChildMajorClass'],
        fetchMajorClassifications,
        (m) => (`${m.code ? m.code + ' ' : ''}${m.name || ''}`).trim() || String(m.id),
        '大分類の取得に失敗しました'
    );
}

/** 子品目の中分類：子品目大分類が選択されていればその配下のみ、未選択なら全件。 */
async function loadChildMiddleClassifications(majorId) {
    await loadSelect(
        'productLabelNewChildMiddleClass',
        () => fetchProductLabelMiddleClassifications(majorId || undefined),
        (m) => (`${m.code ? m.code + ' ' : ''}${m.name || ''}`).trim() || String(m.id),
        '中分類の取得に失敗しました'
    );
}

async function loadWorkcenters() {
    await loadSelect(
        'productLabelNewWorkcenter',
        fetchProductLabelWorkcenters,
        (w) => w.name || String(w.id),
        '作業区の取得に失敗しました'
    );
}

async function loadWarehouses() {
    await loadSelect(
        ['productLabelNewWarehouse', 'productLabelNewChildWarehouse'],
        fetchProductLabelWarehouses,
        (w) => `${w.code ? w.code + ' ' : ''}${w.name || ''}`.trim() || String(w.id),
        '倉庫の取得に失敗しました'
    );
}

function updatePlnSlotLabel() {
    const label = document.getElementById('productLabelNewSlotSelectedLabel');
    if (!label) return;
    const total = plnSlotList.length;
    const sel = plnSelectedSlotCodes.size;
    label.textContent = (total === 0 || sel === 0 || sel === total) ? 'すべて' : `${sel}件選択`;
}

function buildPlnSlotPanel() {
    const container = document.getElementById('productLabelNewSlotOptions');
    if (!container) return;
    container.innerHTML = '';
    plnSlotList.forEach(s => {
        const lbl = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = s.code || '';
        if (plnSelectedSlotCodes.has(s.code)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) {
                if (s.code) plnSelectedSlotCodes.add(s.code);
            } else {
                plnSelectedSlotCodes.delete(s.code);
            }
            updatePlnSlotLabel();
        });
        const text = document.createElement('span');
        text.textContent = s.name || s.code || '';
        lbl.appendChild(cb);
        lbl.appendChild(text);
        container.appendChild(lbl);
    });
    updatePlnSlotLabel();
}

/** 便一覧は納期に依存するため、納期の変更ごとに読み直す（選択はクリア）。 */
async function loadPlnSlots(needDate) {
    plnSelectedSlotCodes = new Set();
    plnSlotList = [];
    const container = document.getElementById('productLabelNewSlotOptions');
    if (container) container.innerHTML = '';
    if (!needDate) {
        updatePlnSlotLabel();
        return;
    }
    try {
        plnSlotList = await fetchProductLabelNewSlots(needDate) || [];
        buildPlnSlotPanel();
    } catch (e) {
        console.error('現品票（新） 便一覧取得エラー:', e);
        updatePlnSlotLabel();
    }
}

function setAllPlnCheckboxes(checked) {
    document.querySelectorAll('.product-label-new-row-check').forEach((el) => { el.checked = checked; });
}

document.getElementById('productLabelNewSearchBtn')?.addEventListener('click', async () => {
    const needDate = document.getElementById('productLabelNewNeedDate')?.value;
    if (!needDate) { alert('納期を入力してください'); return; }

    try {
        const res = await searchProductLabelNew({
            needDate,
            childItemCode: document.getElementById('productLabelNewChildItemCode')?.value || undefined,
            childMajorClassificationId: document.getElementById('productLabelNewChildMajorClass')?.value || undefined,
            childMiddleClassificationId: document.getElementById('productLabelNewChildMiddleClass')?.value || undefined,
            childWarehouseId: document.getElementById('productLabelNewChildWarehouse')?.value || undefined,
            majorClassificationId: document.getElementById('productLabelNewMajorClass')?.value || undefined,
            itemCode: document.getElementById('productLabelNewItemCode')?.value || undefined,
            workcenterId: document.getElementById('productLabelNewWorkcenter')?.value || undefined,
            warehouseId: document.getElementById('productLabelNewWarehouse')?.value || undefined,
            slotCodes: Array.from(plnSelectedSlotCodes),
        });
        plnRows = res.rows || [];
        displayPlnResults(plnRows);
    } catch (error) {
        alert('検索に失敗しました: ' + error.message);
    }
});

const plnSelectAll = document.getElementById('productLabelNewSelectAll');
if (plnSelectAll) {
    plnSelectAll.addEventListener('change', () => setAllPlnCheckboxes(plnSelectAll.checked));
}

function displayPlnResults(rows) {
    const section = document.getElementById('productLabelNewResultsSection');
    const printSection = document.getElementById('productLabelNewPrintSection');
    const countEl = document.getElementById('productLabelNewResultCount');
    const tbody = document.getElementById('productLabelNewResultsBody');
    if (!section || !printSection || !countEl || !tbody) return;

    if (rows.length === 0) {
        alert('該当するデータが見つかりませんでした');
        section.style.display = 'none';
        printSection.style.display = 'none';
        return;
    }

    countEl.textContent = `${rows.length}件`;
    tbody.innerHTML = '';

    rows.forEach((row, idx) => {
        const tr = tbody.insertRow();
        const rowIds = row.order_table_ids || [];

        const tdCb = tr.insertCell();
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.className = 'product-label-new-row-check';
        cb.checked = true;
        cb.dataset.rowIndex = String(idx);
        tdCb.appendChild(cb);

        tr.insertCell().textContent = row.child_item_code || '-';
        tr.insertCell().textContent = row.child_item_name || '-';
        tr.insertCell().textContent = row.depth != null ? String(row.depth) : '-';
        tr.insertCell().textContent = row.parent_item_code || '-';
        tr.insertCell().textContent = row.parent_item_name || '-';
        tr.insertCell().textContent = row.qty != null ? String(row.qty) : '-';
        tr.insertCell().textContent = row.unit_name || '-';
        tr.insertCell().textContent = formatDateYyyymmdd(row.release_date) || '-';
        tr.insertCell().textContent = row.slot_name || row.slot_code || '-';
        tr.insertCell().textContent = row.workcenter_name || '-';
        tr.insertCell().textContent = rowIds.length <= 1
            ? (rowIds[0] > 0 ? String(rowIds[0]) : '-')
            : `合算(${rowIds.length}件)`;

        const tdCount = tr.insertCell();
        const countInput = document.createElement('input');
        countInput.type = 'number';
        countInput.min = '1';
        countInput.max = '99';
        countInput.value = '1';
        countInput.style.cssText = 'width:55px;padding:2px 4px;';
        countInput.className = 'product-label-new-row-count';
        countInput.dataset.rowIndex = String(idx);
        tdCount.appendChild(countInput);
    });

    if (plnSelectAll) plnSelectAll.checked = true;

    section.style.display = 'block';
    printSection.style.display = 'flex';
}

document.addEventListener('DOMContentLoaded', () => {
    loadMajorClassifications();
    loadChildMiddleClassifications('');
    loadWorkcenters();
    loadWarehouses();

    // 子品目大分類を変えたら中分類を絞り直す（選択済み中分類はクリア）
    const childMajorSel = document.getElementById('productLabelNewChildMajorClass');
    childMajorSel?.addEventListener('change', () => {
        loadChildMiddleClassifications(childMajorSel.value || '');
    });

    const needDateInput = document.getElementById('productLabelNewNeedDate');
    const onNeedDateChanged = () => loadPlnSlots(needDateInput?.value || '');
    needDateInput?.addEventListener('change', onNeedDateChanged);
    needDateInput?.addEventListener('input', onNeedDateChanged);
    if (needDateInput?.value) loadPlnSlots(needDateInput.value);

    const slotDisplay = document.getElementById('productLabelNewSlotDisplay');
    slotDisplay?.addEventListener('click', (e) => {
        e.stopPropagation();
        const panel = document.getElementById('productLabelNewSlotOptions');
        if (!panel) return;
        const isHidden = panel.style.display === 'none' || panel.style.display === '';
        panel.style.display = isHidden ? 'block' : 'none';
    });

    document.addEventListener('click', (e) => {
        const dropdown = (e.target instanceof HTMLElement)
            ? e.target.closest('#screen-product-label-new .multi-select-dropdown')
            : null;
        if (!dropdown) {
            const panel = document.getElementById('productLabelNewSlotOptions');
            if (panel) panel.style.display = 'none';
        }
    });
});

/**
 * 印刷用：チェックされた行を {order_table_ids, child_item_code, count} の配列で返す。
 * order_table_ids は合算元の全 ordertableid（サーバ側で数量を合計する）。
 */
export function getSelectedProductLabelNewItems() {
    const items = [];
    document.querySelectorAll('.product-label-new-row-check:checked').forEach((el) => {
        const idx = Number(el.dataset.rowIndex);
        const row = plnRows[idx];
        if (!row) return;
        const ids = (row.order_table_ids || []).filter(id => id > 0);
        if (!ids.length) return;
        const countEl = document.querySelector(`.product-label-new-row-count[data-row-index="${idx}"]`);
        const count = countEl ? Math.max(1, parseInt(countEl.value, 10) || 1) : 1;
        items.push({ order_table_ids: ids, child_item_code: row.child_item_code, count });
    });
    return items;
}
