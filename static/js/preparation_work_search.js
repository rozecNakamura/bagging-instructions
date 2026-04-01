/**
 * 作業前準備書：検索・結果表示・選択
 */
import {
    fetchMajorClassifications,
    fetchMiddleClassifications,
    searchPreparationWork,
    exportPreparationCsv,
    exportPreparationPdf
} from './api.js';
import { openPdfInIframe } from './pdf_generator.js';

let prepGroups = [];

function formatYyyymmdd(yyyymmdd) {
    if (!yyyymmdd || yyyymmdd.length !== 8) return yyyymmdd || '-';
    return `${yyyymmdd.slice(0, 4)}-${yyyymmdd.slice(4, 6)}-${yyyymmdd.slice(6, 8)}`;
}

async function loadPrepMajors() {
    const sel = document.getElementById('prepMajorClass');
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

async function loadPrepMiddles() {
    const majorSel = document.getElementById('prepMajorClass');
    const middleSel = document.getElementById('prepMiddleClass');
    const val = majorSel.value;
    middleSel.innerHTML = '';
    middleSel.disabled = true;
    if (!val) {
        const opt = document.createElement('option');
        opt.value = '';
        opt.textContent = '（大分類を選択してください）';
        middleSel.appendChild(opt);
        return;
    }
    try {
        const list = await fetchMiddleClassifications(Number(val));
        const empty = document.createElement('option');
        empty.value = '';
        empty.textContent = '指定なし（すべて）';
        middleSel.appendChild(empty);
        for (const m of list) {
            const opt = document.createElement('option');
            opt.value = String(m.id);
            const code = m.code ? `${m.code} ` : '';
            opt.textContent = `${code}${m.name || ''}`.trim() || String(m.id);
            middleSel.appendChild(opt);
        }
        middleSel.disabled = false;
    } catch (e) {
        const err = document.createElement('option');
        err.value = '';
        err.textContent = '中分類の取得に失敗しました';
        middleSel.appendChild(err);
        console.error(e);
    }
}

document.addEventListener('DOMContentLoaded', () => {
    loadPrepMajors();
    document.getElementById('prepMajorClass').addEventListener('change', loadPrepMiddles);
});

document.getElementById('prepSearchBtn').addEventListener('click', async () => {
    const needDate = document.getElementById('prepNeedDate').value;
    const slot = document.getElementById('prepSlot').value || '';
    const itemcd = document.getElementById('prepItemCode').value || '';
    const majorIdStr = document.getElementById('prepMajorClass').value;
    const middleIdStr = document.getElementById('prepMiddleClass').value;

    if (!needDate) {
        alert('納期を入力してください');
        return;
    }

    try {
        const res = await searchPreparationWork(
            needDate,
            slot,
            itemcd,
            majorIdStr || undefined,
            middleIdStr || undefined
        );
        prepGroups = res.groups || [];
        displayPrepResults(prepGroups);
    } catch (e) {
        alert('検索に失敗しました: ' + e.message);
        console.error(e);
    }
});

function displayPrepResults(groups) {
    const section = document.getElementById('prepResultsSection');
    const outSection = document.getElementById('prepOutputSection');
    const countEl = document.getElementById('prepResultCount');
    const tbody = document.getElementById('prepResultsBody');

    if (!groups || groups.length === 0) {
        alert('該当するデータが見つかりませんでした');
        section.style.display = 'none';
        outSection.style.display = 'none';
        return;
    }

    countEl.textContent = `${groups.length}件`;
    tbody.innerHTML = '';

    groups.forEach((g, index) => {
        const tr = tbody.insertRow();
        tr.innerHTML = `
            <td><input type="checkbox" class="prep-item-checkbox" data-index="${index}"></td>
            <td>${formatYyyymmdd(g.delvedt) || '-'}</td>
            <td>${g.majorClassificationName || '-'}</td>
            <td>${g.middleClassificationName || '-'}</td>
            <td class="col-num">${g.lineCount ?? 0}</td>
        `;
        tr.style.cursor = 'pointer';
        tr.addEventListener('click', (e) => {
            if (e.target.classList.contains('prep-item-checkbox')) return;
            const cb = tr.querySelector('.prep-item-checkbox');
            if (cb) cb.checked = !cb.checked;
        });
    });

    section.style.display = 'block';
    outSection.style.display = 'block';
    document.getElementById('prepHeaderCheckbox').checked = false;
}

document.getElementById('prepHeaderCheckbox').addEventListener('change', (e) => {
    document.querySelectorAll('.prep-item-checkbox').forEach(cb => { cb.checked = e.target.checked; });
});

document.getElementById('prepSelectAllBtn').addEventListener('click', () => {
    document.querySelectorAll('.prep-item-checkbox').forEach(cb => { cb.checked = true; });
    document.getElementById('prepHeaderCheckbox').checked = true;
});

document.getElementById('prepDeselectAllBtn').addEventListener('click', () => {
    document.querySelectorAll('.prep-item-checkbox').forEach(cb => { cb.checked = false; });
    document.getElementById('prepHeaderCheckbox').checked = false;
});

function getSelectedGroupKeys() {
    const checked = document.querySelectorAll('.prep-item-checkbox:checked');
    const indices = Array.from(checked).map(cb => parseInt(cb.dataset.index, 10));
    const keys = [];
    for (const i of indices) {
        const g = prepGroups[i];
        if (!g || !g.key) continue;
        keys.push(g.key);
    }
    return keys;
}

async function downloadBlob(blob, filename) {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}

document.getElementById('prepCsvBtn').addEventListener('click', async () => {
    const needDate = document.getElementById('prepNeedDate').value;
    const slot = document.getElementById('prepSlot').value || '';
    const itemcd = document.getElementById('prepItemCode').value || '';
    const majorIdStr = document.getElementById('prepMajorClass').value;
    const middleIdStr = document.getElementById('prepMiddleClass').value;
    const keys = getSelectedGroupKeys();
    if (keys.length === 0) {
        alert('出力するグループを選択してください');
        return;
    }
    try {
        let delvedt = needDate;
        if (needDate && needDate.includes('-')) delvedt = needDate.replace(/-/g, '');
        const blob = await exportPreparationCsv(
            {
                delvedt,
                slot,
                itemcd,
                majorId: majorIdStr || null,
                middleId: middleIdStr || null
            },
            keys
        );
        await downloadBlob(blob, '作業前準備書.csv');
    } catch (e) {
        alert('CSV出力に失敗しました: ' + e.message);
        console.error(e);
    }
});

document.getElementById('prepPdfBtn').addEventListener('click', async () => {
    const needDate = document.getElementById('prepNeedDate').value;
    const slot = document.getElementById('prepSlot').value || '';
    const itemcd = document.getElementById('prepItemCode').value || '';
    const majorIdStr = document.getElementById('prepMajorClass').value;
    const middleIdStr = document.getElementById('prepMiddleClass').value;
    const keys = getSelectedGroupKeys();
    if (keys.length === 0) {
        alert('印刷するグループを選択してください');
        return;
    }
    try {
        let delvedt = needDate;
        if (needDate && needDate.includes('-')) delvedt = needDate.replace(/-/g, '');
        const blob = await exportPreparationPdf(
            {
                delvedt,
                slot,
                itemcd,
                majorId: majorIdStr || null,
                middleId: middleIdStr || null
            },
            keys
        );
        openPdfInIframe(blob, '作業前準備書 PDF 印刷');
    } catch (e) {
        alert('PDF出力に失敗しました: ' + e.message);
        console.error(e);
    }
}
);

