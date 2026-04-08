/**
 * 袋詰：製造日・品目合算検索・チェックボックス
 */

import { searchBaggingGroups } from './api.js';

/** @type {object[]} */
export let baggingSearchGroups = [];

function formatPrddtDisplay(prddt) {
    if (!prddt || prddt.length !== 8) return prddt || '-';
    return `${prddt.slice(0, 4)}-${prddt.slice(4, 6)}-${prddt.slice(6, 8)}`;
}

document.getElementById('searchBtn').addEventListener('click', async () => {
    const productionDate = document.getElementById('productionDate').value;
    const productCode = document.getElementById('productCode').value;

    if (!productionDate) {
        alert('製造日を入力してください');
        return;
    }

    try {
        const response = await searchBaggingGroups(productionDate, productCode);
        baggingSearchGroups = response.groups || [];
        await displayResults(baggingSearchGroups);
    } catch (error) {
        alert('検索に失敗しました: ' + error.message);
    }
});

async function displayResults(groups) {
    const resultsSection = document.getElementById('resultsSection');
    const resultCount = document.getElementById('resultCount');
    const tbody = document.getElementById('resultsBody');

    if (groups.length === 0) {
        alert('該当するデータが見つかりませんでした');
        resultsSection.style.display = 'none';
        const { closeBaggingRegistration } = await import('./bagging_registration.js');
        closeBaggingRegistration();
        return;
    }

    resultCount.textContent = `${groups.length}件`;
    tbody.innerHTML = '';

    groups.forEach((g, index) => {
        const row = tbody.insertRow();
        const unitLabel = g.unit_name || g.unit_code || '-';
        row.innerHTML = `
            <td><input type="checkbox" class="bagging-group-checkbox" data-group-index="${index}"></td>
            <td>${formatPrddtDisplay(g.prddt)}</td>
            <td>${g.itemcd || '-'}</td>
            <td>${g.itemnm || '-'}</td>
            <td>${g.total_jobordqun ?? 0}</td>
            <td>${unitLabel}</td>
        `;
        row.style.cursor = 'pointer';
        row.addEventListener('click', (e) => {
            if (e.target.classList.contains('bagging-group-checkbox')) return;
            const cb = row.querySelector('.bagging-group-checkbox');
            if (cb) cb.checked = !cb.checked;
        });
    });

    resultsSection.style.display = 'block';
}

document.getElementById('headerCheckbox').addEventListener('change', (e) => {
    document.querySelectorAll('.bagging-group-checkbox').forEach(cb => {
        cb.checked = e.target.checked;
    });
});

document.getElementById('selectAllBtn').addEventListener('click', () => {
    document.querySelectorAll('.bagging-group-checkbox').forEach(cb => { cb.checked = true; });
    document.getElementById('headerCheckbox').checked = true;
});

document.getElementById('deselectAllBtn').addEventListener('click', () => {
    document.querySelectorAll('.bagging-group-checkbox').forEach(cb => { cb.checked = false; });
    document.getElementById('headerCheckbox').checked = false;
});

/** 選択されたグループ（単一）の line_prkeys。複数選択時は null */
export function getSelectedBaggingGroup() {
    const checked = Array.from(document.querySelectorAll('.bagging-group-checkbox:checked'));
    if (checked.length !== 1) return null;
    const idx = parseInt(checked[0].dataset.groupIndex, 10);
    if (Number.isNaN(idx) || !baggingSearchGroups[idx]) return null;
    return baggingSearchGroups[idx];
}
