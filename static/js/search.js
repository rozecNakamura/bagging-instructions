/**
 * 袋詰：製造日・品目合算検索（行クリックで投入量登録を開く）
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
        row.classList.add('bagging-result-row');
        row.tabIndex = 0;
        row.setAttribute('role', 'button');
        row.setAttribute('aria-label', `袋詰投入量登録を開く: ${g.itemcd || ''}`);
        const unitLabel = g.unit_name || g.unit_code || '-';
        row.innerHTML = `
            <td>${formatPrddtDisplay(g.prddt)}</td>
            <td>${g.itemcd || '-'}</td>
            <td>${g.itemnm || '-'}</td>
            <td>${g.total_jobordqun ?? 0}</td>
            <td>${unitLabel}</td>
        `;
        row.style.cursor = 'pointer';
        row.dataset.groupIndex = String(index);

        const openForRow = async () => {
            tbody.querySelectorAll('.bagging-result-row--active').forEach((r) => {
                r.classList.remove('bagging-result-row--active');
            });
            row.classList.add('bagging-result-row--active');
            const group = baggingSearchGroups[index];
            if (!group) return;
            const { openBaggingRegistrationForGroup } = await import('./bagging_registration.js');
            await openBaggingRegistrationForGroup(group);
        };

        row.addEventListener('click', () => {
            void openForRow();
        });
        row.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                void openForRow();
            }
        });
    });

    resultsSection.style.display = 'block';
}
