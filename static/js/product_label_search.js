/**
 * 現品票印刷：マスタ読込・検索・結果表示（明細行・チェック選択）
 */
import {
    fetchMajorClassifications,
    fetchProductLabelWorkcenters,
    fetchProductLabelWarehouses,
    searchProductLabel,
} from './api.js';


let productLabelRows = [];

function formatDateYyyymmdd(yyyymmdd) {
    if (!yyyymmdd || yyyymmdd.length !== 8) return yyyymmdd || '-';
    return `${yyyymmdd.slice(0, 4)}-${yyyymmdd.slice(4, 6)}-${yyyymmdd.slice(6, 8)}`;
}

async function loadSelect(selectId, fetcher, labelFn, errorText) {
    const sel = document.getElementById(selectId);
    if (!sel) return;
    try {
        const list = await fetcher();
        sel.innerHTML = '';
        const empty = document.createElement('option');
        empty.value = '';
        empty.textContent = '指定なし（すべて）';
        sel.appendChild(empty);
        for (const item of list) {
            const opt = document.createElement('option');
            opt.value = String(item.id);
            opt.textContent = labelFn(item);
            sel.appendChild(opt);
        }
    } catch (e) {
        sel.innerHTML = `<option value="">${errorText}</option>`;
        console.error(e);
    }
}

async function loadMajorClassifications() {
    await loadSelect(
        'productLabelMajorClass',
        fetchMajorClassifications,
        (m) => `${m.code ? m.code + ' ' : ''}${m.name || ''}`.trim() || String(m.id),
        '大分類の取得に失敗しました'
    );
}

async function loadWorkcenters() {
    await loadSelect(
        'productLabelWorkcenter',
        fetchProductLabelWorkcenters,
        (w) => w.name || String(w.id),
        '作業区の取得に失敗しました'
    );
}

async function loadWarehouses() {
    await loadSelect(
        'productLabelWarehouse',
        fetchProductLabelWarehouses,
        (w) => `${w.code ? w.code + ' ' : ''}${w.name || ''}`.trim() || String(w.id),
        '倉庫の取得に失敗しました'
    );
}

function setAllProductLabelCheckboxes(checked) {
    document.querySelectorAll('.product-label-row-check').forEach((el) => { el.checked = checked; });
}

document.getElementById('productLabelSearchBtn').addEventListener('click', async () => {
    const needDate = document.getElementById('productLabelNeedDate').value;
    if (!needDate) { alert('納期を入力してください'); return; }

    const majorId = document.getElementById('productLabelMajorClass')?.value;
    const itemCode = document.getElementById('productLabelItemCode')?.value;
    const workcenterId = document.getElementById('productLabelWorkcenter')?.value;
    const warehouseId = document.getElementById('productLabelWarehouse')?.value;

    try {
        const res = await searchProductLabel({
            needDate,
            majorClassificationId: majorId || undefined,
            itemCode: itemCode || undefined,
            workcenterId: workcenterId || undefined,
            warehouseId: warehouseId || undefined,
        });
        productLabelRows = res.rows || [];
        displayProductLabelResults(productLabelRows);
    } catch (error) {
        alert('検索に失敗しました: ' + error.message);
    }
});

const productLabelSelectAll = document.getElementById('productLabelSelectAll');
if (productLabelSelectAll) {
    productLabelSelectAll.addEventListener('change', () => {
        setAllProductLabelCheckboxes(productLabelSelectAll.checked);
    });
}

function displayProductLabelResults(rows) {
    const section = document.getElementById('productLabelResultsSection');
    const printSection = document.getElementById('productLabelPrintSection');
    const countEl = document.getElementById('productLabelResultCount');
    const tbody = document.getElementById('productLabelResultsBody');

    if (rows.length === 0) {
        alert('該当するデータが見つかりませんでした');
        section.style.display = 'none';
        printSection.style.display = 'none';
        return;
    }

    countEl.textContent = `${rows.length}件`;
    tbody.innerHTML = '';

    rows.forEach((row) => {
        const tr = tbody.insertRow();

        const tdCb = tr.insertCell();
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.className = 'product-label-row-check';
        cb.checked = true;
        cb.dataset.orderTableId = String(row.order_table_id);
        tdCb.appendChild(cb);

        tr.insertCell().textContent = row.order_table_id != null ? String(row.order_table_id) : '-';
        tr.insertCell().textContent = formatDateYyyymmdd(row.release_date) || '-';
        tr.insertCell().textContent = row.item_code || '-';
        tr.insertCell().textContent = row.item_name || '-';
        tr.insertCell().textContent = row.child_count != null ? String(row.child_count) : '-';
        tr.insertCell().textContent = row.qty != null ? String(row.qty) : '-';
        tr.insertCell().textContent = row.workcenter_name || '-';

        const tdCount = tr.insertCell();
        const countInput = document.createElement('input');
        countInput.type = 'number';
        countInput.min = '1';
        countInput.max = '99';
        countInput.value = '1';
        countInput.style.cssText = 'width:55px;padding:2px 4px;';
        countInput.className = 'product-label-row-count';
        countInput.dataset.orderTableId = String(row.order_table_id);
        tdCount.appendChild(countInput);
    });

    if (productLabelSelectAll) productLabelSelectAll.checked = true;

    section.style.display = 'block';
    printSection.style.display = 'flex';
}

document.addEventListener('DOMContentLoaded', () => {
    loadMajorClassifications();
    loadWorkcenters();
    loadWarehouses();
});

/** 印刷用：選択された ordertableid */
export function getSelectedProductLabelOrderTableIds() {
    const ids = [];
    document.querySelectorAll('.product-label-row-check:checked').forEach((el) => {
        const id = Number(el.dataset.orderTableId, 10);
        if (Number.isFinite(id) && id > 0) ids.push(id);
    });
    return ids;
}

/** 印刷用：選択された {id, count} の配列（行ごとの枚数を含む）。*/
export function getSelectedProductLabelItems() {
    const items = [];
    document.querySelectorAll('.product-label-row-check:checked').forEach((el) => {
        const id = Number(el.dataset.orderTableId, 10);
        if (!Number.isFinite(id) || id <= 0) return;
        const countEl = document.querySelector(`.product-label-row-count[data-order-table-id="${id}"]`);
        const count = countEl ? Math.max(1, parseInt(countEl.value, 10) || 1) : 1;
        items.push({ id, count });
    });
    return items;
}
