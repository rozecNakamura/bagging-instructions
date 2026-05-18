/**
 * カット前準備書・現品ラベル：検索・結果表示・選択・出力
 */
import {
    fetchCutPreparationWorkcenters,
    fetchCutPreparationManufacturingRoutes,
    searchCutPreparation,
    exportCutPreparationExcel,
    exportCutPreparationProductLabelPdf
} from './api.js';
import { openPdfInIframe } from './pdf_generator.js';

let cutGroups = [];
let cutWorkcenterList = [];
let cutManufacturingRouteList = [];
let cutSelectedWorkcenters = new Set();
let cutSelectedManufacturingRoutes = new Set();

function formatYyyymmdd(yyyymmdd) {
    if (!yyyymmdd || yyyymmdd.length !== 8) return yyyymmdd || '-';
    return `${yyyymmdd.slice(0, 4)}-${yyyymmdd.slice(4, 6)}-${yyyymmdd.slice(6, 8)}`;
}

function getCutNeedDateValue() {
    return document.getElementById('cutNeedDate')?.value?.trim() ?? '';
}

function updateCutWorkcenterSummary() {
    const label = document.getElementById('cutWorkcenterSelectedLabel');
    if (!label) return;
    if (cutSelectedWorkcenters.size === 0) {
        label.textContent = '未選択';
    } else if (cutSelectedWorkcenters.size === cutWorkcenterList.length && cutWorkcenterList.length > 0) {
        label.textContent = 'すべて選択';
    } else {
        label.textContent = `${cutSelectedWorkcenters.size}件選択`;
    }
}

function updateCutManufacturingRouteSummary() {
    const label = document.getElementById('cutManufacturingRouteSelectedLabel');
    if (!label) return;
    const needDate = document.getElementById('cutNeedDate')?.value;
    if (!needDate) {
        label.textContent = '製造日を選択してください';
        return;
    }
    if (cutManufacturingRouteList.length === 0) {
        label.textContent = '該当する製造便がありません';
        return;
    }
    if (cutSelectedManufacturingRoutes.size === 0) {
        label.textContent = '未選択';
    } else if (cutSelectedManufacturingRoutes.size === cutManufacturingRouteList.length) {
        label.textContent = 'すべて選択';
    } else {
        label.textContent = `${cutSelectedManufacturingRoutes.size}件選択`;
    }
}

function buildCutWorkcenterPanel() {
    const container = document.getElementById('cutWorkcenterOptions');
    if (!container) return;
    container.innerHTML = '';
    cutWorkcenterList.forEach(w => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = String(w.id);
        if (cutSelectedWorkcenters.has(cb.value)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) cutSelectedWorkcenters.add(cb.value);
            else cutSelectedWorkcenters.delete(cb.value);
            updateCutWorkcenterSummary();
        });
        const text = document.createElement('span');
        text.textContent = w.name || '';
        label.appendChild(cb);
        label.appendChild(text);
        container.appendChild(label);
    });
    updateCutWorkcenterSummary();
}

function buildCutManufacturingRoutePanel() {
    const container = document.getElementById('cutManufacturingRouteOptions');
    if (!container) return;
    container.innerHTML = '';
    cutManufacturingRouteList.forEach(s => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = s.code || '';
        if (cutSelectedManufacturingRoutes.has(cb.value)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) cutSelectedManufacturingRoutes.add(cb.value);
            else cutSelectedManufacturingRoutes.delete(cb.value);
            updateCutManufacturingRouteSummary();
        });
        const text = document.createElement('span');
        text.textContent = s.name || s.code || '';
        label.appendChild(cb);
        label.appendChild(text);
        container.appendChild(label);
    });
    updateCutManufacturingRouteSummary();
}

async function refreshCutManufacturingRoutesFromNeedDate() {
    const needDate = document.getElementById('cutNeedDate')?.value || '';
    cutSelectedManufacturingRoutes = new Set();
    cutManufacturingRouteList = [];
    const container = document.getElementById('cutManufacturingRouteOptions');
    if (container) container.innerHTML = '';

    if (!needDate) {
        updateCutManufacturingRouteSummary();
        return;
    }

    try {
        cutManufacturingRouteList = await fetchCutPreparationManufacturingRoutes(needDate) || [];
        buildCutManufacturingRoutePanel();
    } catch (e) {
        console.error('カット前準備書 製造便取得エラー:', e);
        cutManufacturingRouteList = [];
        updateCutManufacturingRouteSummary();
    }
}

function getSelectedCutWorkcenterIds() {
    return Array.from(cutSelectedWorkcenters.values()).map(v => Number(v));
}

function getSelectedCutManufacturingRouteCodes() {
    return Array.from(cutSelectedManufacturingRoutes.values()).filter(c => c && String(c).trim());
}

function buildCutSearchOptions() {
    const itemcd = document.getElementById('cutItemCode')?.value?.trim() ?? '';
    return {
        itemcd,
        manufacturingRouteCodes: getSelectedCutManufacturingRouteCodes(),
        workcenterIds: getSelectedCutWorkcenterIds()
    };
}

function getSelectedCutGroupKeys() {
    const checked = document.querySelectorAll('.cut-item-checkbox:checked');
    const indices = Array.from(checked).map(cb => parseInt(cb.dataset.index, 10));
    const keys = [];
    for (const i of indices) {
        const g = cutGroups[i];
        if (!g || !g.key) continue;
        keys.push(g.key);
    }
    return keys;
}

function displayCutResults(groups) {
    const section = document.getElementById('cutResultsSection');
    const outSection = document.getElementById('cutOutputSection');
    const countEl = document.getElementById('cutResultCount');
    const tbody = document.getElementById('cutResultsBody');

    if (!section || !outSection || !countEl || !tbody) {
        console.error('カット前準備書: 結果エリアのDOMが見つかりません');
        return;
    }

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
            <td><input type="checkbox" class="cut-item-checkbox" data-index="${index}"></td>
            <td>${formatYyyymmdd(g.delvedt) || '-'}</td>
            <td>${g.manufacturingRouteName || g.manufacturingRouteCode || '-'}</td>
            <td class="col-num">${g.lineCount ?? 0}</td>
        `;
        tr.style.cursor = 'pointer';
        tr.addEventListener('click', (e) => {
            if (e.target.classList.contains('cut-item-checkbox')) return;
            const cb = tr.querySelector('.cut-item-checkbox');
            if (cb) cb.checked = !cb.checked;
        });
    });

    section.style.display = 'block';
    outSection.style.display = 'flex';
    const headerCb = document.getElementById('cutHeaderCheckbox');
    if (headerCb) headerCb.checked = false;
}

document.addEventListener('DOMContentLoaded', () => {
    (async () => {
        try {
            const wcs = await fetchCutPreparationWorkcenters();
            cutWorkcenterList = wcs || [];
            buildCutWorkcenterPanel();
        } catch (e) {
            console.error('カット前準備書 作業区マスタ取得エラー:', e);
        }
    })();

    const needDateInput = document.getElementById('cutNeedDate');
    needDateInput?.addEventListener('change', refreshCutManufacturingRoutesFromNeedDate);
    needDateInput?.addEventListener('input', refreshCutManufacturingRoutesFromNeedDate);
    if (getCutNeedDateValue()) {
        refreshCutManufacturingRoutesFromNeedDate();
    }

    const workcenterDisplay = document.getElementById('cutWorkcenterDisplay');
    const manufacturingRouteDisplay = document.getElementById('cutManufacturingRouteDisplay');

    function closeAllCutPanels() {
        ['cutWorkcenterOptions', 'cutManufacturingRouteOptions'].forEach(id => {
            const el = document.getElementById(id);
            if (el) el.style.display = 'none';
        });
    }

    if (workcenterDisplay) {
        workcenterDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('cutWorkcenterOptions');
            if (!panel) return;
            const isHidden = panel.style.display === 'none' || panel.style.display === '';
            closeAllCutPanels();
            panel.style.display = isHidden ? 'block' : 'none';
        });
    }
    if (manufacturingRouteDisplay) {
        manufacturingRouteDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('cutManufacturingRouteOptions');
            if (!panel) return;
            if (!document.getElementById('cutNeedDate')?.value) {
                alert('先に製造日を選択してください');
                return;
            }
            const isHidden = panel.style.display === 'none' || panel.style.display === '';
            closeAllCutPanels();
            panel.style.display = isHidden ? 'block' : 'none';
        });
    }

    document.addEventListener('click', (e) => {
        const dropdown = (e.target instanceof HTMLElement)
            ? e.target.closest('#screen-cut-preparation .multi-select-dropdown')
            : null;
        if (!dropdown) {
            closeAllCutPanels();
        }
    });

    const cutSearchBtn = document.getElementById('cutSearchBtn');
    if (cutSearchBtn) {
        cutSearchBtn.addEventListener('click', async () => {
            const needDate = getCutNeedDateValue();
            if (!needDate) {
                alert('製造日を入力してください');
                return;
            }
            try {
                let delvedt = needDate;
                if (needDate.includes('-')) delvedt = needDate.replace(/-/g, '');
                const opts = buildCutSearchOptions();
                const res = await searchCutPreparation(delvedt, opts);
                cutGroups = res.groups || [];
                displayCutResults(cutGroups);
            } catch (e) {
                alert('検索に失敗しました: ' + e.message);
                console.error(e);
            }
        });
    }

    const cutHeaderCheckbox = document.getElementById('cutHeaderCheckbox');
    cutHeaderCheckbox?.addEventListener('change', (e) => {
        document.querySelectorAll('.cut-item-checkbox').forEach(cb => { cb.checked = e.target.checked; });
    });

    document.getElementById('cutSelectAllBtn')?.addEventListener('click', () => {
        document.querySelectorAll('.cut-item-checkbox').forEach(cb => { cb.checked = true; });
        const hb = document.getElementById('cutHeaderCheckbox');
        if (hb) hb.checked = true;
    });

    document.getElementById('cutDeselectAllBtn')?.addEventListener('click', () => {
        document.querySelectorAll('.cut-item-checkbox').forEach(cb => { cb.checked = false; });
        const hb = document.getElementById('cutHeaderCheckbox');
        if (hb) hb.checked = false;
    });

    document.getElementById('cutExcelBtn')?.addEventListener('click', async () => {
        const needDate = getCutNeedDateValue();
        const keys = getSelectedCutGroupKeys();
        if (keys.length === 0) {
            alert('出力するグループを選択してください');
            return;
        }
        try {
            let delvedt = needDate;
            if (needDate && needDate.includes('-')) delvedt = needDate.replace(/-/g, '');
            const opts = buildCutSearchOptions();
            const blob = await exportCutPreparationExcel({ delvedt, ...opts }, keys);
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = 'カット前準備書.xlsx';
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
        } catch (e) {
            alert('Excel出力に失敗しました: ' + e.message);
            console.error(e);
        }
    });

    document.getElementById('cutProductLabelBtn')?.addEventListener('click', async () => {
        const needDate = getCutNeedDateValue();
        const keys = getSelectedCutGroupKeys();
        if (keys.length === 0) {
            alert('印刷するグループを選択してください');
            return;
        }
        const labelCountEl = document.getElementById('cutLabelCount');
        const labelCount = labelCountEl ? Math.max(1, parseInt(labelCountEl.value, 10) || 1) : 1;
        try {
            let delvedt = needDate;
            if (needDate && needDate.includes('-')) delvedt = needDate.replace(/-/g, '');
            const opts = buildCutSearchOptions();
            const blob = await exportCutPreparationProductLabelPdf(
                { delvedt, ...opts },
                keys,
                labelCount,
                'cut'
            );
            openPdfInIframe(blob, '現品ラベル PDF 印刷');
        } catch (e) {
            alert('現品ラベル出力に失敗しました: ' + e.message);
            console.error(e);
        }
    });
});
