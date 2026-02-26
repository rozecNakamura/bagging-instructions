/**
 * 汁仕分表：検索・結果表示・選択
 */
import { searchJuice } from './api.js';

let juiceSearchResults = [];

document.getElementById('juiceSearchBtn').addEventListener('click', async () => {
    const eatingDate = document.getElementById('juiceEatingDate').value;
    const itemCode = document.getElementById('juiceItemCode').value?.trim() || '';

    if (!eatingDate) {
        alert('喫食日を入力してください');
        return;
    }

    try {
        const response = await searchJuice(eatingDate, itemCode || undefined);
        juiceSearchResults = response.items || [];
        displayJuiceResults(juiceSearchResults);
    } catch (error) {
        alert('検索に失敗しました: ' + error.message);
    }
});

function displayJuiceResults(items) {
    const section = document.getElementById('juiceResultsSection');
    const printSection = document.getElementById('juicePrintSection');
    const countEl = document.getElementById('juiceResultCount');
    const tbody = document.getElementById('juiceResultsBody');

    if (items.length === 0) {
        alert('該当するデータが見つかりませんでした');
        section.style.display = 'none';
        printSection.style.display = 'none';
        return;
    }

    countEl.textContent = `${items.length}件`;
    tbody.innerHTML = '';

    const shptmDisplay = (item) => item.shptm_name || item.shptm || '-';

    items.forEach(item => {
        const row = tbody.insertRow();
        row.innerHTML = `
            <td><input type="checkbox" class="juice-item-checkbox" data-prkey="${item.prkey}"></td>
            <td>${formatDate(item.delvedt) || '-'}</td>
            <td>${shptmDisplay(item)}</td>
            <td>${item.itemcd || '-'}</td>
            <td>${item.jobordmernm || '-'}</td>
        `;
        row.style.cursor = 'pointer';
        row.addEventListener('click', (e) => {
            if (e.target.classList.contains('juice-item-checkbox')) return;
            const cb = row.querySelector('.juice-item-checkbox');
            if (cb) cb.checked = !cb.checked;
        });
    });

    section.style.display = 'block';
    printSection.style.display = 'block';
    document.getElementById('juiceHeaderCheckbox').checked = false;
}

function formatDate(yyyymmdd) {
    if (!yyyymmdd || yyyymmdd.length !== 8) return yyyymmdd;
    return `${yyyymmdd.slice(0, 4)}-${yyyymmdd.slice(4, 6)}-${yyyymmdd.slice(6, 8)}`;
}

// ヘッダーチェックボックス
document.getElementById('juiceHeaderCheckbox').addEventListener('change', (e) => {
    document.querySelectorAll('.juice-item-checkbox').forEach(cb => { cb.checked = e.target.checked; });
});

document.getElementById('juiceSelectAllBtn').addEventListener('click', () => {
    document.querySelectorAll('.juice-item-checkbox').forEach(cb => { cb.checked = true; });
    document.getElementById('juiceHeaderCheckbox').checked = true;
});

document.getElementById('juiceDeselectAllBtn').addEventListener('click', () => {
    document.querySelectorAll('.juice-item-checkbox').forEach(cb => { cb.checked = false; });
    document.getElementById('juiceHeaderCheckbox').checked = false;
});

/**
 * 選択された行データを取得（PDF印刷・汁仕分表.rxzテンプレート用）
 * @returns {{ delvedt: string, shptmDisplay: string, itemcd: string, jobordmernm: string, shpctrnm: string, jobordqun: number, addinfo02: string }[]}
 */
export function getSelectedJuiceRows() {
    const checked = document.querySelectorAll('.juice-item-checkbox:checked');
    const prkeys = new Set(Array.from(checked).map(cb => parseInt(cb.dataset.prkey, 10)));
    return juiceSearchResults
        .filter(item => prkeys.has(item.prkey))
        .map(item => ({
            delvedt: formatDate(item.delvedt) || '-',
            shptmDisplay: item.shptm_name || item.shptm || '-',
            itemcd: item.itemcd || '-',
            jobordmernm: item.jobordmernm || '-',
            shpctrnm: item.shpctrnm || '-',
            jobordqun: Number(item.jobordqun) || 0,
            addinfo02: item.addinfo02 || ''
        }));
}
