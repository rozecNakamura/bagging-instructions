/**
 * 作業前準備書（計画オーダー）：検索・結果表示・選択
 * 作業区・倉庫・分類マスタは作業前準備書の API を共用し、製造便・検索・CSV は planning-preparation-work API を使用する。
 */
import {
    fetchMajorClassifications,
    fetchMiddleClassifications,
    fetchPreparationWorkcenters,
    fetchPreparationWarehouses,
    fetchPreparationManufacturingRoutes,
    searchPreparationWork,
    exportPreparationCsv
} from './api.js';

const PLAN_PREP_API_PATH = 'planning-preparation-work';

let planPrepGroups = [];
let planPrepMajorList = [];
let planPrepMiddleList = [];
let planPrepSelectedMajors = new Set();
let planPrepSelectedMiddles = new Set();

let planPrepWorkcenterList = [];
let planPrepWarehouseList = [];
let planPrepManufacturingRouteList = [];
let planPrepSelectedWorkcenters = new Set();
let planPrepSelectedWarehouses = new Set();
let planPrepSelectedManufacturingRoutes = new Set();

function formatYyyymmdd(yyyymmdd) {
    if (!yyyymmdd || yyyymmdd.length !== 8) return yyyymmdd || '-';
    return `${yyyymmdd.slice(0, 4)}-${yyyymmdd.slice(4, 6)}-${yyyymmdd.slice(6, 8)}`;
}

function getPlanPrepNeedDateValue() {
    return document.getElementById('planPrepNeedDate')?.value?.trim() ?? '';
}

async function loadPlanPrepMajors() {
    try {
        const list = await fetchMajorClassifications();
        planPrepMajorList = list || [];
        buildPlanPrepMajorPanel();
    } catch (e) {
        console.error(e);
    }
}

async function loadPlanPrepMiddles() {
    const majorIds = Array.from(planPrepSelectedMajors.values()).map(Number);
    planPrepSelectedMiddles.clear();
    if (!majorIds.length) {
        planPrepMiddleList = [];
        buildPlanPrepMiddlePanel();
        return;
    }
    try {
        const list = await fetchMiddleClassifications(majorIds);
        planPrepMiddleList = list || [];
        buildPlanPrepMiddlePanel();
    } catch (e) {
        console.error(e);
    }
}

function updatePlanPrepSelectedSummary() {
    const majorLabel = document.getElementById('planPrepMajorSelectedLabel');
    const middleLabel = document.getElementById('planPrepMiddleSelectedLabel');

    if (majorLabel) {
        if (planPrepSelectedMajors.size === 0) {
            majorLabel.textContent = '未選択';
        } else if (planPrepSelectedMajors.size === planPrepMajorList.length) {
            majorLabel.textContent = 'すべて選択';
        } else {
            majorLabel.textContent = `${planPrepSelectedMajors.size}件選択`;
        }
    }

    if (middleLabel) {
        if (planPrepSelectedMiddles.size === 0) {
            middleLabel.textContent = '未選択';
        } else if (planPrepSelectedMiddles.size === planPrepMiddleList.length) {
            middleLabel.textContent = 'すべて選択';
        } else {
            middleLabel.textContent = `${planPrepSelectedMiddles.size}件選択`;
        }
    }
}

function updatePlanPrepWorkcenterSummary() {
    const label = document.getElementById('planPrepWorkcenterSelectedLabel');
    if (!label) return;
    if (planPrepSelectedWorkcenters.size === 0) {
        label.textContent = '未選択';
    } else if (planPrepSelectedWorkcenters.size === planPrepWorkcenterList.length && planPrepWorkcenterList.length > 0) {
        label.textContent = 'すべて選択';
    } else {
        label.textContent = `${planPrepSelectedWorkcenters.size}件選択`;
    }
}

function updatePlanPrepWarehouseSummary() {
    const label = document.getElementById('planPrepWarehouseSelectedLabel');
    if (!label) return;
    if (planPrepSelectedWarehouses.size === 0) {
        label.textContent = '未選択';
    } else if (planPrepSelectedWarehouses.size === planPrepWarehouseList.length && planPrepWarehouseList.length > 0) {
        label.textContent = 'すべて選択';
    } else {
        label.textContent = `${planPrepSelectedWarehouses.size}件選択`;
    }
}

function updatePlanPrepManufacturingRouteSummary() {
    const label = document.getElementById('planPrepManufacturingRouteSelectedLabel');
    if (!label) return;
    const needDate = document.getElementById('planPrepNeedDate')?.value;
    if (!needDate) {
        label.textContent = '納期を選択してください';
        return;
    }
    if (planPrepManufacturingRouteList.length === 0) {
        label.textContent = '該当する製造便がありません';
        return;
    }
    if (planPrepSelectedManufacturingRoutes.size === 0) {
        label.textContent = '未選択';
    } else if (planPrepSelectedManufacturingRoutes.size === planPrepManufacturingRouteList.length) {
        label.textContent = 'すべて選択';
    } else {
        label.textContent = `${planPrepSelectedManufacturingRoutes.size}件選択`;
    }
}

function buildPlanPrepMajorPanel() {
    const container = document.getElementById('planPrepMajorOptions');
    if (!container) return;
    container.innerHTML = '';
    planPrepMajorList.forEach(m => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = String(m.id);
        if (planPrepSelectedMajors.has(cb.value)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) {
                planPrepSelectedMajors.add(cb.value);
            } else {
                planPrepSelectedMajors.delete(cb.value);
            }
            updatePlanPrepSelectedSummary();
            loadPlanPrepMiddles();
        });
        const text = document.createElement('span');
        text.textContent = m.code ? `${m.code} ${m.name}` : (m.name || '');
        label.appendChild(cb);
        label.appendChild(text);
        container.appendChild(label);
    });
    updatePlanPrepSelectedSummary();
}

function buildPlanPrepMiddlePanel() {
    const container = document.getElementById('planPrepMiddleOptions');
    if (!container) return;
    container.innerHTML = '';
    planPrepMiddleList.forEach(m => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = String(m.id);
        if (planPrepSelectedMiddles.has(cb.value)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) {
                planPrepSelectedMiddles.add(cb.value);
            } else {
                planPrepSelectedMiddles.delete(cb.value);
            }
            updatePlanPrepSelectedSummary();
        });
        const text = document.createElement('span');
        text.textContent = m.code ? `${m.code} ${m.name}` : (m.name || '');
        label.appendChild(cb);
        label.appendChild(text);
        container.appendChild(label);
    });
    updatePlanPrepSelectedSummary();
}

function buildPlanPrepWorkcenterPanel() {
    const container = document.getElementById('planPrepWorkcenterOptions');
    if (!container) return;
    container.innerHTML = '';
    planPrepWorkcenterList.forEach(w => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = String(w.id);
        if (planPrepSelectedWorkcenters.has(cb.value)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) planPrepSelectedWorkcenters.add(cb.value);
            else planPrepSelectedWorkcenters.delete(cb.value);
            updatePlanPrepWorkcenterSummary();
        });
        const text = document.createElement('span');
        text.textContent = w.code ? `${w.code} ${w.name}` : (w.name || '');
        label.appendChild(cb);
        label.appendChild(text);
        container.appendChild(label);
    });
    updatePlanPrepWorkcenterSummary();
}

function buildPlanPrepWarehousePanel() {
    const container = document.getElementById('planPrepWarehouseOptions');
    if (!container) return;
    container.innerHTML = '';
    planPrepWarehouseList.forEach(w => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = String(w.id);
        if (planPrepSelectedWarehouses.has(cb.value)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) planPrepSelectedWarehouses.add(cb.value);
            else planPrepSelectedWarehouses.delete(cb.value);
            updatePlanPrepWarehouseSummary();
        });
        const text = document.createElement('span');
        text.textContent = w.code && w.name ? `${w.code} ${w.name}` : (w.name || w.code || '');
        label.appendChild(cb);
        label.appendChild(text);
        container.appendChild(label);
    });
    updatePlanPrepWarehouseSummary();
}

function buildPlanPrepManufacturingRoutePanel() {
    const container = document.getElementById('planPrepManufacturingRouteOptions');
    if (!container) return;
    container.innerHTML = '';
    planPrepManufacturingRouteList.forEach(s => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = s.code || '';
        if (planPrepSelectedManufacturingRoutes.has(cb.value)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) planPrepSelectedManufacturingRoutes.add(cb.value);
            else planPrepSelectedManufacturingRoutes.delete(cb.value);
            updatePlanPrepManufacturingRouteSummary();
        });
        const text = document.createElement('span');
        text.textContent = s.name || s.code || '';
        label.appendChild(cb);
        label.appendChild(text);
        container.appendChild(label);
    });
    updatePlanPrepManufacturingRouteSummary();
}

async function refreshPlanPrepManufacturingRoutesFromNeedDate() {
    const needDate = document.getElementById('planPrepNeedDate')?.value || '';
    planPrepSelectedManufacturingRoutes = new Set();
    planPrepManufacturingRouteList = [];
    const container = document.getElementById('planPrepManufacturingRouteOptions');
    if (container) container.innerHTML = '';

    if (!needDate) {
        updatePlanPrepManufacturingRouteSummary();
        return;
    }

    try {
        planPrepManufacturingRouteList = await fetchPreparationManufacturingRoutes(needDate, PLAN_PREP_API_PATH) || [];
        buildPlanPrepManufacturingRoutePanel();
    } catch (e) {
        console.error('作業前準備書（計画オーダー） 製造便取得エラー:', e);
        planPrepManufacturingRouteList = [];
        updatePlanPrepManufacturingRouteSummary();
    }
}

function getSelectedPlanPrepWorkcenterIds() {
    return Array.from(planPrepSelectedWorkcenters.values()).map(v => Number(v));
}

function getSelectedPlanPrepWarehouseIds() {
    return Array.from(planPrepSelectedWarehouses.values()).map(v => Number(v));
}

function getSelectedPlanPrepManufacturingRouteCodes() {
    return Array.from(planPrepSelectedManufacturingRoutes.values()).filter(c => c && String(c).trim());
}

function buildPlanPrepSearchOptions() {
    const itemcd = document.getElementById('planPrepItemCode')?.value?.trim() ?? '';
    const majorIds = Array.from(planPrepSelectedMajors.values()).map(Number);
    const middleIds = Array.from(planPrepSelectedMiddles.values());
    const allMiddlesSelected = planPrepMiddleList.length > 0 && planPrepSelectedMiddles.size === planPrepMiddleList.length;
    const allWorkcentersSelected = planPrepWorkcenterList.length > 0 && planPrepSelectedWorkcenters.size === planPrepWorkcenterList.length;
    const allWarehousesSelected = planPrepWarehouseList.length > 0 && planPrepSelectedWarehouses.size === planPrepWarehouseList.length;
    return {
        itemcd,
        majorIds,
        middleId: (!allMiddlesSelected && middleIds.length) ? Number(middleIds[0]) : undefined,
        manufacturingRouteCodes: getSelectedPlanPrepManufacturingRouteCodes(),
        workcenterIds: allWorkcentersSelected ? [] : getSelectedPlanPrepWorkcenterIds(),
        warehouseIds: allWarehousesSelected ? [] : getSelectedPlanPrepWarehouseIds()
    };
}

document.addEventListener('DOMContentLoaded', () => {
    loadPlanPrepMajors();

    (async () => {
        try {
            const [wcs, whs] = await Promise.all([
                fetchPreparationWorkcenters(),
                fetchPreparationWarehouses()
            ]);
            planPrepWorkcenterList = wcs || [];
            planPrepWarehouseList = whs || [];
            buildPlanPrepWorkcenterPanel();
            buildPlanPrepWarehousePanel();
        } catch (e) {
            console.error('作業前準備書（計画オーダー） マスタ取得エラー:', e);
        }
    })();

    const needDateInput = document.getElementById('planPrepNeedDate');
    const onPlanPrepNeedDateChanged = () => {
        refreshPlanPrepManufacturingRoutesFromNeedDate();
    };
    needDateInput?.addEventListener('change', onPlanPrepNeedDateChanged);
    needDateInput?.addEventListener('input', onPlanPrepNeedDateChanged);
    if (getPlanPrepNeedDateValue()) {
        refreshPlanPrepManufacturingRoutesFromNeedDate();
    }

    const majorDisplay = document.getElementById('planPrepMajorDisplay');
    const middleDisplay = document.getElementById('planPrepMiddleDisplay');
    const workcenterDisplay = document.getElementById('planPrepWorkcenterDisplay');
    const warehouseDisplay = document.getElementById('planPrepWarehouseDisplay');
    const manufacturingRouteDisplay = document.getElementById('planPrepManufacturingRouteDisplay');

    function closeAllPlanPrepPanels() {
        ['planPrepMajorOptions', 'planPrepMiddleOptions', 'planPrepWorkcenterOptions', 'planPrepWarehouseOptions', 'planPrepManufacturingRouteOptions'].forEach(id => {
            const el = document.getElementById(id);
            if (el) el.style.display = 'none';
        });
    }

    if (majorDisplay) {
        majorDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('planPrepMajorOptions');
            if (!panel) return;
            const isHidden = panel.style.display === 'none' || panel.style.display === '';
            closeAllPlanPrepPanels();
            panel.style.display = isHidden ? 'block' : 'none';
        });
    }
    if (middleDisplay) {
        middleDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('planPrepMiddleOptions');
            if (!panel) return;
            const isHidden = panel.style.display === 'none' || panel.style.display === '';
            closeAllPlanPrepPanels();
            panel.style.display = isHidden ? 'block' : 'none';
        });
    }
    if (workcenterDisplay) {
        workcenterDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('planPrepWorkcenterOptions');
            if (!panel) return;
            const isHidden = panel.style.display === 'none' || panel.style.display === '';
            closeAllPlanPrepPanels();
            panel.style.display = isHidden ? 'block' : 'none';
        });
    }
    if (warehouseDisplay) {
        warehouseDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('planPrepWarehouseOptions');
            if (!panel) return;
            const isHidden = panel.style.display === 'none' || panel.style.display === '';
            closeAllPlanPrepPanels();
            panel.style.display = isHidden ? 'block' : 'none';
        });
    }
    if (manufacturingRouteDisplay) {
        manufacturingRouteDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('planPrepManufacturingRouteOptions');
            if (!panel) return;
            if (!document.getElementById('planPrepNeedDate')?.value) {
                alert('先に納期を選択してください');
                return;
            }
            const isHidden = panel.style.display === 'none' || panel.style.display === '';
            closeAllPlanPrepPanels();
            panel.style.display = isHidden ? 'block' : 'none';
        });
    }

    document.addEventListener('click', (e) => {
        const dropdown = (e.target instanceof HTMLElement)
            ? e.target.closest('#screen-planning-preparation-work .multi-select-dropdown')
            : null;
        if (!dropdown) {
            closeAllPlanPrepPanels();
        }
    });

    const planPrepSearchBtn = document.getElementById('planPrepSearchBtn');
    if (planPrepSearchBtn) {
        planPrepSearchBtn.addEventListener('click', async () => {
            const needDate = getPlanPrepNeedDateValue();

            if (!needDate) {
                alert('納期を入力してください');
                return;
            }

            try {
                const res = await searchPreparationWork(needDate, buildPlanPrepSearchOptions(), PLAN_PREP_API_PATH);
                planPrepGroups = res.groups || [];
                displayPlanPrepResults(planPrepGroups);
            } catch (e) {
                alert('検索に失敗しました: ' + e.message);
                console.error(e);
            }
        });
    }

    const planPrepHeaderCheckbox = document.getElementById('planPrepHeaderCheckbox');
    planPrepHeaderCheckbox?.addEventListener('change', (e) => {
        document.querySelectorAll('.plan-prep-item-checkbox').forEach(cb => { cb.checked = e.target.checked; });
    });

    document.getElementById('planPrepSelectAllBtn')?.addEventListener('click', () => {
        document.querySelectorAll('.plan-prep-item-checkbox').forEach(cb => { cb.checked = true; });
        const hb = document.getElementById('planPrepHeaderCheckbox');
        if (hb) hb.checked = true;
    });

    document.getElementById('planPrepDeselectAllBtn')?.addEventListener('click', () => {
        document.querySelectorAll('.plan-prep-item-checkbox').forEach(cb => { cb.checked = false; });
        const hb = document.getElementById('planPrepHeaderCheckbox');
        if (hb) hb.checked = false;
    });

    document.getElementById('planPrepCsvBtn')?.addEventListener('click', async () => {
        const needDate = getPlanPrepNeedDateValue();
        const keys = getSelectedGroupKeys();
        if (keys.length === 0) {
            alert('出力するグループを選択してください');
            return;
        }
        try {
            let delvedt = needDate;
            if (needDate && needDate.includes('-')) delvedt = needDate.replace(/-/g, '');
            const opts = buildPlanPrepSearchOptions();
            const blob = await exportPreparationCsv(
                {
                    delvedt,
                    ...opts
                },
                keys,
                PLAN_PREP_API_PATH
            );
            await downloadBlob(blob, '作業前準備書（計画オーダー）.csv');
        } catch (e) {
            alert('CSV出力に失敗しました: ' + e.message);
            console.error(e);
        }
    });
});

function displayPlanPrepResults(groups) {
    const section = document.getElementById('planPrepResultsSection');
    const outSection = document.getElementById('planPrepOutputSection');
    const countEl = document.getElementById('planPrepResultCount');
    const tbody = document.getElementById('planPrepResultsBody');

    if (!section || !outSection || !countEl || !tbody) {
        console.error('作業前準備書（計画オーダー）: 結果エリアの DOM が見つかりません');
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
            <td><input type="checkbox" class="plan-prep-item-checkbox" data-index="${index}"></td>
            <td>${formatYyyymmdd(g.delvedt) || '-'}</td>
            <td>${majorDisplay}</td>
            <td>${middleDisplay}</td>
            <td class="col-num">${g.lineCount ?? 0}</td>
        `;
        tr.style.cursor = 'pointer';
        tr.addEventListener('click', (e) => {
            if (e.target.classList.contains('plan-prep-item-checkbox')) return;
            const cb = tr.querySelector('.plan-prep-item-checkbox');
            if (cb) cb.checked = !cb.checked;
        });
    });

    section.style.display = 'block';
    outSection.style.display = 'flex';
    const headerCb = document.getElementById('planPrepHeaderCheckbox');
    if (headerCb) headerCb.checked = false;
}

function getSelectedGroupKeys() {
    const checked = document.querySelectorAll('.plan-prep-item-checkbox:checked');
    const indices = Array.from(checked).map(cb => parseInt(cb.dataset.index, 10));
    const keys = [];
    for (const i of indices) {
        const g = planPrepGroups[i];
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
