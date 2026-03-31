/**
 * 現品票印刷：大分類読込・検索・結果表示
 */
import { fetchMajorClassifications, searchProductLabel } from './api.js';

let productLabelRows = [];

function formatNeedDateYyyymmdd(yyyymmdd) {
    if (!yyyymmdd || yyyymmdd.length !== 8) return yyyymmdd || '-';
    return `${yyyymmdd.slice(0, 4)}-${yyyymmdd.slice(4, 6)}-${yyyymmdd.slice(6, 8)}`;
}

async function loadMajorClassifications() {
    const sel = document.getElementById('productLabelMajorClass');
    try {
        const list = await fetchMajorClassifications();
        sel.innerHTML = '';
        const empty = document.createElement('option');
        empty.value = '';
        empty.textContent = '指定なし（すべて）';
        sel.appendChild(empty);
        for (const m of list) {
            const opt = document.createElement('option');
            opt.value = String(m.id);
            const code = m.code ? `${m.code} ` : '';
            opt.textContent = `${code}${m.name || ''}`.trim() || String(m.id);
            sel.appendChild(opt);
        }
    } catch (e) {
        sel.innerHTML = '';
        const err = document.createElement('option');
        err.value = '';
        err.textContent = '大分類の取得に失敗しました';
        sel.appendChild(err);
        console.error(e);
    }
}

document.getElementById('productLabelSearchBtn').addEventListener('click', async () => {
    const needDate = document.getElementById('productLabelNeedDate').value;
    const majorId = document.getElementById('productLabelMajorClass').value;

    if (!needDate) {
        alert('納期を入力してください');
        return;
    }

    try {
        const res = await searchProductLabel(needDate, majorId || undefined);
        productLabelRows = res.rows || [];
        displayProductLabelResults(productLabelRows);
    } catch (error) {
        alert('検索に失敗しました: ' + error.message);
    }
});

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
        const td1 = tr.insertCell();
        const td2 = tr.insertCell();
        const td3 = tr.insertCell();
        td1.textContent = formatNeedDateYyyymmdd(row.need_date) || '-';
        td2.textContent = row.item_display || '-';
        td3.textContent = row.qty != null ? String(row.qty) : '-';
    });

    section.style.display = 'block';
    printSection.style.display = 'block';
}

document.addEventListener('DOMContentLoaded', () => {
    loadMajorClassifications();
});

/** 印刷用（product_label_print.js） */
export function getProductLabelRows() {
    return productLabelRows;
}
