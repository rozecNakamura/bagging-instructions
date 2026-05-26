/**
 * 作業前準備書：検索・結果表示・選択
 */
import {
    fetchMajorClassifications,
    fetchMiddleClassifications,
    fetchPreparationWorkcenters,
    fetchPreparationWarehouses,
    fetchPreparationManufacturingRoutes,
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

let prepWorkcenterList = [];
let prepWarehouseList = [];
let prepManufacturingRouteList = [];
let prepSelectedWorkcenters = new Set();
let prepSelectedWarehouses = new Set();
let prepSelectedManufacturingRoutes = new Set();

function formatYyyymmdd(yyyymmdd) {
    if (!yyyymmdd || yyyymmdd.length !== 8) return yyyymmdd || '-';
    return `${yyyymmdd.slice(0, 4)}-${yyyymmdd.slice(4, 6)}-${yyyymmdd.slice(6, 8)}`;
}

function getPrepNeedDateValue() {
    return document.getElementById('prepNeedDate')?.value?.trim() ?? '';
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
    const majorIds = Array.from(prepSelectedMajors.values()).map(Number);
    prepSelectedMiddles.clear();
    if (!majorIds.length) {
        prepMiddleList = [];
        buildPrepMiddlePanel();
        return;
    }
    try {
        const list = await fetchMiddleClassifications(majorIds);
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

function updatePrepWorkcenterSummary() {
    const label = document.getElementById('prepWorkcenterSelectedLabel');
    if (!label) return;
    if (prepSelectedWorkcenters.size === 0) {
        label.textContent = '未選択';
    } else if (prepSelectedWorkcenters.size === prepWorkcenterList.length && prepWorkcenterList.length > 0) {
        label.textContent = 'すべて選択';
    } else {
        label.textContent = `${prepSelectedWorkcenters.size}件選択`;
    }
}

function updatePrepWarehouseSummary() {
    const label = document.getElementById('prepWarehouseSelectedLabel');
    if (!label) return;
    if (prepSelectedWarehouses.size === 0) {
        label.textContent = '未選択';
    } else if (prepSelectedWarehouses.size === prepWarehouseList.length && prepWarehouseList.length > 0) {
        label.textContent = 'すべて選択';
    } else {
        label.textContent = `${prepSelectedWarehouses.size}件選択`;
    }
}

function updatePrepManufacturingRouteSummary() {
    const label = document.getElementById('prepManufacturingRouteSelectedLabel');
    if (!label) return;
    const needDate = document.getElementById('prepNeedDate')?.value;
    if (!needDate) {
        label.textContent = '納期を選択してください';
        return;
    }
    if (prepManufacturingRouteList.length === 0) {
        label.textContent = '該当する製造便がありません';
        return;
    }
    if (prepSelectedManufacturingRoutes.size === 0) {
        label.textContent = '未選択';
    } else if (prepSelectedManufacturingRoutes.size === prepManufacturingRouteList.length) {
        label.textContent = 'すべて選択';
    } else {
        label.textContent = `${prepSelectedManufacturingRoutes.size}件選択`;
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
            loadPrepMiddles();
        });
        const text = document.createElement('span');
        text.textContent = m.code ? `${m.code} ${m.name}` : (m.name || '');
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
        text.textContent = m.code ? `${m.code} ${m.name}` : (m.name || '');
        label.appendChild(cb);
        label.appendChild(text);
        container.appendChild(label);
    });
    updatePrepSelectedSummary();
}

function buildPrepWorkcenterPanel() {
    const container = document.getElementById('prepWorkcenterOptions');
    if (!container) return;
    container.innerHTML = '';
    prepWorkcenterList.forEach(w => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = String(w.id);
        if (prepSelectedWorkcenters.has(cb.value)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) prepSelectedWorkcenters.add(cb.value);
            else prepSelectedWorkcenters.delete(cb.value);
            updatePrepWorkcenterSummary();
        });
        const text = document.createElement('span');
        text.textContent = w.code ? `${w.code} ${w.name}` : (w.name || '');
        label.appendChild(cb);
        label.appendChild(text);
        container.appendChild(label);
    });
    updatePrepWorkcenterSummary();
}

function buildPrepWarehousePanel() {
    const container = document.getElementById('prepWarehouseOptions');
    if (!container) return;
    container.innerHTML = '';
    prepWarehouseList.forEach(w => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = String(w.id);
        if (prepSelectedWarehouses.has(cb.value)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) prepSelectedWarehouses.add(cb.value);
            else prepSelectedWarehouses.delete(cb.value);
            updatePrepWarehouseSummary();
        });
        const text = document.createElement('span');
        text.textContent = w.code && w.name ? `${w.code} ${w.name}` : (w.name || w.code || '');
        label.appendChild(cb);
        label.appendChild(text);
        container.appendChild(label);
    });
    updatePrepWarehouseSummary();
}

function buildPrepManufacturingRoutePanel() {
    const container = document.getElementById('prepManufacturingRouteOptions');
    if (!container) return;
    container.innerHTML = '';
    prepManufacturingRouteList.forEach(s => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = s.code || '';
        if (prepSelectedManufacturingRoutes.has(cb.value)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) prepSelectedManufacturingRoutes.add(cb.value);
            else prepSelectedManufacturingRoutes.delete(cb.value);
            updatePrepManufacturingRouteSummary();
        });
        const text = document.createElement('span');
        text.textContent = s.name || s.code || '';
        label.appendChild(cb);
        label.appendChild(text);
        container.appendChild(label);
    });
    updatePrepManufacturingRouteSummary();
}

async function refreshPrepManufacturingRoutesFromNeedDate() {
    const needDate = document.getElementById('prepNeedDate')?.value || '';
    prepSelectedManufacturingRoutes = new Set();
    prepManufacturingRouteList = [];
    const container = document.getElementById('prepManufacturingRouteOptions');
    if (container) container.innerHTML = '';

    if (!needDate) {
        updatePrepManufacturingRouteSummary();
        return;
    }

    try {
        prepManufacturingRouteList = await fetchPreparationManufacturingRoutes(needDate) || [];
        buildPrepManufacturingRoutePanel();
    } catch (e) {
        console.error('作業前準備書 製造便取得エラー:', e);
        prepManufacturingRouteList = [];
        updatePrepManufacturingRouteSummary();
    }
}

function getSelectedPrepWorkcenterIds() {
    return Array.from(prepSelectedWorkcenters.values()).map(v => Number(v));
}

function getSelectedPrepWarehouseIds() {
    return Array.from(prepSelectedWarehouses.values()).map(v => Number(v));
}

function getSelectedPrepManufacturingRouteCodes() {
    return Array.from(prepSelectedManufacturingRoutes.values()).filter(c => c && String(c).trim());
}

function buildPrepSearchOptions() {
    const itemcd = document.getElementById('prepItemCode')?.value?.trim() ?? '';
    const majorIds = Array.from(prepSelectedMajors.values()).map(Number);
    const middleIds = Array.from(prepSelectedMiddles.values());
    const allMiddlesSelected = prepMiddleList.length > 0 && prepSelectedMiddles.size === prepMiddleList.length;
    const allWorkcentersSelected = prepWorkcenterList.length > 0 && prepSelectedWorkcenters.size === prepWorkcenterList.length;
    const allWarehousesSelected = prepWarehouseList.length > 0 && prepSelectedWarehouses.size === prepWarehouseList.length;
    return {
        itemcd,
        majorIds,
        middleId: (!allMiddlesSelected && middleIds.length) ? Number(middleIds[0]) : undefined,
        manufacturingRouteCodes: getSelectedPrepManufacturingRouteCodes(),
        workcenterIds: allWorkcentersSelected ? [] : getSelectedPrepWorkcenterIds(),
        warehouseIds: allWarehousesSelected ? [] : getSelectedPrepWarehouseIds()
    };
}

document.addEventListener('DOMContentLoaded', () => {
    loadPrepMajors();

    (async () => {
        try {
            const [wcs, whs] = await Promise.all([
                fetchPreparationWorkcenters(),
                fetchPreparationWarehouses()
            ]);
            prepWorkcenterList = wcs || [];
            prepWarehouseList = whs || [];
            buildPrepWorkcenterPanel();
            buildPrepWarehousePanel();
        } catch (e) {
            console.error('作業前準備書 マスタ取得エラー:', e);
        }
    })();

    const needDateInput = document.getElementById('prepNeedDate');
    const onPrepNeedDateChanged = () => {
        refreshPrepManufacturingRoutesFromNeedDate();
    };
    needDateInput?.addEventListener('change', onPrepNeedDateChanged);
    needDateInput?.addEventListener('input', onPrepNeedDateChanged);
    if (getPrepNeedDateValue()) {
        refreshPrepManufacturingRoutesFromNeedDate();
    }

    const majorDisplay = document.getElementById('prepMajorDisplay');
    const middleDisplay = document.getElementById('prepMiddleDisplay');
    const workcenterDisplay = document.getElementById('prepWorkcenterDisplay');
    const warehouseDisplay = document.getElementById('prepWarehouseDisplay');
    const manufacturingRouteDisplay = document.getElementById('prepManufacturingRouteDisplay');

    function closeAllPrepPanels() {
        ['prepMajorOptions', 'prepMiddleOptions', 'prepWorkcenterOptions', 'prepWarehouseOptions', 'prepManufacturingRouteOptions'].forEach(id => {
            const el = document.getElementById(id);
            if (el) el.style.display = 'none';
        });
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
    if (workcenterDisplay) {
        workcenterDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('prepWorkcenterOptions');
            if (!panel) return;
            const isHidden = panel.style.display === 'none' || panel.style.display === '';
            closeAllPrepPanels();
            panel.style.display = isHidden ? 'block' : 'none';
        });
    }
    if (warehouseDisplay) {
        warehouseDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('prepWarehouseOptions');
            if (!panel) return;
            const isHidden = panel.style.display === 'none' || panel.style.display === '';
            closeAllPrepPanels();
            panel.style.display = isHidden ? 'block' : 'none';
        });
    }
    if (manufacturingRouteDisplay) {
        manufacturingRouteDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('prepManufacturingRouteOptions');
            if (!panel) return;
            if (!document.getElementById('prepNeedDate')?.value) {
                alert('先に納期を選択してください');
                return;
            }
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

    const prepSearchBtn = document.getElementById('prepSearchBtn');
    if (prepSearchBtn) {
        prepSearchBtn.addEventListener('click', async () => {
            const needDate = getPrepNeedDateValue();

            if (!needDate) {
                alert('納期を入力してください');
                return;
            }

            try {
                const res = await searchPreparationWork(needDate, buildPrepSearchOptions());
                prepGroups = res.groups || [];
                displayPrepResults(prepGroups);
            } catch (e) {
                alert('検索に失敗しました: ' + e.message);
                console.error(e);
            }
        });
    }

    const prepHeaderCheckbox = document.getElementById('prepHeaderCheckbox');
    prepHeaderCheckbox?.addEventListener('change', (e) => {
        document.querySelectorAll('.prep-item-checkbox').forEach(cb => { cb.checked = e.target.checked; });
    });

    document.getElementById('prepSelectAllBtn')?.addEventListener('click', () => {
        document.querySelectorAll('.prep-item-checkbox').forEach(cb => { cb.checked = true; });
        const hb = document.getElementById('prepHeaderCheckbox');
        if (hb) hb.checked = true;
    });

    document.getElementById('prepDeselectAllBtn')?.addEventListener('click', () => {
        document.querySelectorAll('.prep-item-checkbox').forEach(cb => { cb.checked = false; });
        const hb = document.getElementById('prepHeaderCheckbox');
        if (hb) hb.checked = false;
    });

    document.getElementById('prepCsvBtn')?.addEventListener('click', async () => {
        const needDate = getPrepNeedDateValue();
        const keys = getSelectedGroupKeys();
        if (keys.length === 0) {
            alert('出力するグループを選択してください');
            return;
        }
        try {
            let delvedt = needDate;
            if (needDate && needDate.includes('-')) delvedt = needDate.replace(/-/g, '');
            const opts = buildPrepSearchOptions();
            const blob = await exportPreparationCsv(
                {
                    delvedt,
                    ...opts
                },
                keys
            );
            await downloadBlob(blob, '作業前準備書.csv');
        } catch (e) {
            alert('CSV出力に失敗しました: ' + e.message);
            console.error(e);
        }
    });

    document.getElementById('prepPdfBtn')?.addEventListener('click', async () => {
        const needDate = getPrepNeedDateValue();
        const keys = getSelectedGroupKeys();
        if (keys.length === 0) {
            alert('印刷するグループを選択してください');
            return;
        }
        try {
            let delvedt = needDate;
            if (needDate && needDate.includes('-')) delvedt = needDate.replace(/-/g, '');
            const opts = buildPrepSearchOptions();
            const blob = await exportPreparationPdf(
                {
                    delvedt,
                    ...opts
                },
                keys
            );
            openPdfInIframe(blob, '作業前準備書 PDF 印刷');
        } catch (e) {
            alert('PDF出力に失敗しました: ' + e.message);
            console.error(e);
        }
    });
});

function displayPrepResults(groups) {
    const section = document.getElementById('prepResultsSection');
    const outSection = document.getElementById('prepOutputSection');
    const countEl = document.getElementById('prepResultCount');
    const tbody = document.getElementById('prepResultsBody');

    if (!section || !outSection || !countEl || !tbody) {
        console.error('作業前準備書: 結果エリアの DOM が見つかりません');
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
        const majorDisplay = (g.key?.majorClassificationCode && g.majorClassificationName)
            ? `${g.key.majorClassificationCode} ${g.majorClassificationName}`
            : (g.majorClassificationName || g.key?.majorClassificationCode || '-');
        const middleDisplay = (g.key?.middleClassificationCode && g.middleClassificationName)
            ? `${g.key.middleClassificationCode} ${g.middleClassificationName}`
            : (g.middleClassificationName || g.key?.middleClassificationCode || '-');
        tr.innerHTML = `
            <td><input type="checkbox" class="prep-item-checkbox" data-index="${index}"></td>
            <td>${formatYyyymmdd(g.delvedt) || '-'}</td>
            <td>${majorDisplay}</td>
            <td>${middleDisplay}</td>
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
    const headerCb = document.getElementById('prepHeaderCheckbox');
    if (headerCb) headerCb.checked = false;
}

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
