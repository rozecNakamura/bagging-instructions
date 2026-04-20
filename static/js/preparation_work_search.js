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
let prepMajorList = [];
let prepMiddleList = [];
let prepSelectedMajors = new Set();
let prepSelectedMiddles = new Set();

function formatYyyymmdd(yyyymmdd) {
    if (!yyyymmdd || yyyymmdd.length !== 8) return yyyymmdd || '-';
    return `${yyyymmdd.slice(0, 4)}-${yyyymmdd.slice(4, 6)}-${yyyymmdd.slice(6, 8)}`;
}

async function loadPrepMajors() {
    try {
        const list = await fetchMajorClassifications();
        prepMajorList = list || [];
        buildPrepMajorPanel();
    } catch (e) {
        console.error(e);
    }
}

async function loadPrepMiddles() {
    const majorIds = Array.from(prepSelectedMajors.values());
    if (!majorIds.length) {
        prepMiddleList = [];
        buildPrepMiddlePanel();
        return;
    }
    try {
        // 代表として先頭の大分類IDで中分類を取得（必要に応じて拡張）
        const firstId = Number(majorIds[0]);
        const list = await fetchMiddleClassifications(firstId);
        prepMiddleList = list || [];
        buildPrepMiddlePanel();
    } catch (e) {
        console.error(e);
    }
}

function updatePrepSelectedSummary() {
    const majorLabel = document.getElementById('prepMajorSelectedLabel');
    const middleLabel = document.getElementById('prepMiddleSelectedLabel');

    if (majorLabel) {
        if (prepSelectedMajors.size === 0) {
            majorLabel.textContent = '未選択';
        } else if (prepSelectedMajors.size === prepMajorList.length) {
            majorLabel.textContent = 'すべて選択';
        } else {
            majorLabel.textContent = `${prepSelectedMajors.size}件選択`;
        }
    }

    if (middleLabel) {
        if (prepSelectedMiddles.size === 0) {
            middleLabel.textContent = '未選択';
        } else if (prepSelectedMiddles.size === prepMiddleList.length) {
            middleLabel.textContent = 'すべて選択';
        } else {
            middleLabel.textContent = `${prepSelectedMiddles.size}件選択`;
        }
    }
}

function buildPrepMajorPanel() {
    const container = document.getElementById('prepMajorOptions');
    if (!container) return;
    container.innerHTML = '';
    prepMajorList.forEach(m => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = String(m.id);
        if (prepSelectedMajors.has(cb.value)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) {
                prepSelectedMajors.add(cb.value);
            } else {
                prepSelectedMajors.delete(cb.value);
            }
            updatePrepSelectedSummary();
            // 大分類が変わったら中分類も更新
            loadPrepMiddles();
        });
        const text = document.createElement('span');
        // ドロップダウンには名称のみ表示（コードは表示しない）
        text.textContent = m.name || '';
        label.appendChild(cb);
        label.appendChild(text);
        container.appendChild(label);
    });
    updatePrepSelectedSummary();
}

function buildPrepMiddlePanel() {
    const container = document.getElementById('prepMiddleOptions');
    if (!container) return;
    container.innerHTML = '';
    prepMiddleList.forEach(m => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = String(m.id);
        if (prepSelectedMiddles.has(cb.value)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) {
                prepSelectedMiddles.add(cb.value);
            } else {
                prepSelectedMiddles.delete(cb.value);
            }
            updatePrepSelectedSummary();
        });
        const text = document.createElement('span');
        // ドロップダウンには名称のみ表示（コードは表示しない）
        text.textContent = m.name || '';
        label.appendChild(cb);
        label.appendChild(text);
        container.appendChild(label);
    });
    updatePrepSelectedSummary();
}

document.addEventListener('DOMContentLoaded', () => {
    loadPrepMajors();
    const majorDisplay = document.getElementById('prepMajorDisplay');
    const middleDisplay = document.getElementById('prepMiddleDisplay');

    function closeAllPrepPanels() {
        const majorPanel = document.getElementById('prepMajorOptions');
        const middlePanel = document.getElementById('prepMiddleOptions');
        if (majorPanel) majorPanel.style.display = 'none';
        if (middlePanel) middlePanel.style.display = 'none';
    }

    if (majorDisplay) {
        majorDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('prepMajorOptions');
            if (!panel) return;
            const isHidden = panel.style.display === 'none' || panel.style.display === '';
            closeAllPrepPanels();
            panel.style.display = isHidden ? 'block' : 'none';
        });
    }
    if (middleDisplay) {
        middleDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('prepMiddleOptions');
            if (!panel) return;
            const isHidden = panel.style.display === 'none' || panel.style.display === '';
            closeAllPrepPanels();
            panel.style.display = isHidden ? 'block' : 'none';
        });
    }

    document.addEventListener('click', (e) => {
        const dropdown = (e.target instanceof HTMLElement)
            ? e.target.closest('#screen-preparation-work .multi-select-dropdown')
            : null;
        if (!dropdown) {
            closeAllPrepPanels();
        }
    });
});

document.getElementById('prepSearchBtn').addEventListener('click', async () => {
    const needDate = document.getElementById('prepNeedDate').value;
    const slot = document.getElementById('prepSlot').value || '';
    const itemcd = document.getElementById('prepItemCode').value || '';
    const majorIds = Array.from(prepSelectedMajors.values());
    const middleIds = Array.from(prepSelectedMiddles.values());

    if (!needDate) {
        alert('納期を入力してください');
        return;
    }

    try {
        const res = await searchPreparationWork(
            needDate,
            slot,
            itemcd,
            majorIds.length ? majorIds[0] : undefined,
            middleIds.length ? middleIds[0] : undefined
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
    outSection.style.display = 'flex';
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
    const majorIds = Array.from(prepSelectedMajors.values());
    const middleIds = Array.from(prepSelectedMiddles.values());
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
                majorId: majorIds.length ? majorIds[0] : null,
                middleId: middleIds.length ? middleIds[0] : null
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
    const majorIds = Array.from(prepSelectedMajors.values());
    const middleIds = Array.from(prepSelectedMiddles.values());
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
                majorId: majorIds.length ? majorIds[0] : null,
                middleId: middleIds.length ? middleIds[0] : null
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

