/**
 * 袋詰：製造日・品目合算検索（行クリックで投入量登録を開く）
 */

import { searchBaggingGroups, buildBaggingSearchExportUrl, downloadBaggingPreparationExcel } from './api.js';

/** @type {object[]} */
export let baggingSearchGroups = [];

/** 最後の検索条件（Excelエクスポートで再利用） */
let lastSearchParams = { productionDate: '', productCode: '', isComplete: '' };

function formatPrddtDisplay(prddt) {
    if (!prddt || prddt.length !== 8) return prddt || '-';
    return `${prddt.slice(0, 4)}-${prddt.slice(4, 6)}-${prddt.slice(6, 8)}`;
}

async function performSearch() {
    const productionDate = document.getElementById('productionDate').value;
    const productCode = document.getElementById('productCode').value;
    const isComplete = document.getElementById('completionFilter')?.value || '';

    if (!productionDate) {
        alert('製造日を入力してください');
        return;
    }

    lastSearchParams = { productionDate, productCode, isComplete };
    const response = await searchBaggingGroups(productionDate, productCode, isComplete);
    baggingSearchGroups = response.groups || [];
    await displayResults(baggingSearchGroups);
}

document.getElementById('searchBtn').addEventListener('click', async () => {
    try {
        await performSearch();
    } catch (error) {
        alert('検索に失敗しました: ' + error.message);
    }
});

export async function refreshBaggingSearch() {
    try {
        await performSearch();
    } catch { /* silent refresh */ }
}

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
        const statusLabel = g.is_printed ? '完了'
            : g.is_instruction_printed ? '指示書済み'
            : g.is_label_printed ? 'ラベル済み'
            : '未完了';
        row.innerHTML = `
            <td>${formatPrddtDisplay(g.prddt)}</td>
            <td>${g.itemcd || '-'}</td>
            <td>${g.itemnm || '-'}</td>
            <td>${g.total_jobordqun ?? 0}</td>
            <td>${unitLabel}</td>
            <td>${statusLabel}</td>
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

document.getElementById('baggingExcelExportBtn')?.addEventListener('click', () => {
    const { productionDate, productCode, isComplete } = lastSearchParams;
    if (!productionDate) { alert('先に検索してください。'); return; }
    const url = buildBaggingSearchExportUrl(productionDate, productCode, isComplete);
    window.location.href = url;
});

async function handlePrepExcelDownload(aggregate) {
    const { productionDate } = lastSearchParams;
    if (!productionDate || baggingSearchGroups.length === 0) {
        alert('先に検索してください。');
        return;
    }
    const allPrkeys = baggingSearchGroups.flatMap(g => g.line_prkeys || []);
    try {
        const blob = await downloadBaggingPreparationExcel(productionDate, allPrkeys, aggregate);
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        const suffix = aggregate ? '_数量合算' : '';
        a.download = `作業前準備書${suffix}_${productionDate}.xlsx`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    } catch (e) {
        alert('Excelダウンロードに失敗しました: ' + e.message);
    }
}

document.getElementById('baggingRegPrepExcelBtn')?.addEventListener('click', () => {
    void handlePrepExcelDownload(false);
});

document.getElementById('baggingRegPrepExcelAggBtn')?.addEventListener('click', () => {
    void handlePrepExcelDownload(true);
});
