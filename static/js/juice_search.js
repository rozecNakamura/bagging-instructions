/**
 * 汁仕分表：検索・結果表示・選択（喫食日・喫食時間・品目でグループ化表示）
 */
import { searchJuice } from './api.js';

/** 検索結果グループ一覧（喫食日・喫食時間・品目でまとめた単位） */
let juiceSearchGroups = [];

document.getElementById('juiceSearchBtn').addEventListener('click', async () => {
    const eatingDate = document.getElementById('juiceEatingDate').value;
    const itemCode = document.getElementById('juiceItemCode').value?.trim() || '';

    if (!eatingDate) {
        alert('喫食日を入力してください');
        return;
    }

    try {
        const response = await searchJuice(eatingDate, itemCode || undefined);
        juiceSearchGroups = response.groups || [];
        displayJuiceResults(juiceSearchGroups);
    } catch (error) {
        alert('検索に失敗しました: ' + error.message);
    }
});

function displayJuiceResults(groups) {
    const section = document.getElementById('juiceResultsSection');
    const printSection = document.getElementById('juicePrintSection');
    const countEl = document.getElementById('juiceResultCount');
    const tbody = document.getElementById('juiceResultsBody');

    if (groups.length === 0) {
        alert('該当するデータが見つかりませんでした');
        section.style.display = 'none';
        printSection.style.display = 'none';
        return;
    }

    countEl.textContent = `${groups.length}件`;
    tbody.innerHTML = '';

    groups.forEach((group, index) => {
        const row = tbody.insertRow();
        const locationCount = group.locations ? group.locations.length : 0;
        row.innerHTML = `
            <td><input type="checkbox" class="juice-item-checkbox" data-group-index="${index}"></td>
            <td>${formatDate(group.delvedt) || '-'}</td>
            <td>${group.shptm_display || '-'}</td>
            <td>${group.itemcd || '-'}</td>
            <td>${group.jobordmernm || '-'}</td>
            <td>${locationCount}</td>
        `;
        row.style.cursor = 'pointer';
        row.addEventListener('click', (e) => {
            if (e.target.classList.contains('juice-item-checkbox')) return;
            const cb = row.querySelector('.juice-item-checkbox');
            if (cb) cb.checked = !cb.checked;
        });
    });

    section.style.display = 'block';
    printSection.style.display = 'flex';
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
 * 選択されたグループを、PDF用に「納入場所ごと1行」に展開して返す（品目は共通で、施設を並べて表示するため）
 * @returns {{ delvedt: string, shptmDisplay: string, jobordmernm: string, shpctrnm: string, jobordqun: number, addinfo02: string }[]}
 */
export function getSelectedJuiceRows() {
    const checked = document.querySelectorAll('.juice-item-checkbox:checked');
    const indices = new Set(Array.from(checked).map(cb => parseInt(cb.dataset.groupIndex, 10)));
    const rows = [];
    for (const i of indices) {
        const group = juiceSearchGroups[i];
        if (!group || !group.locations || group.locations.length === 0) continue;
        const delvedt = formatDate(group.delvedt) || '-';
        const shptmDisplay = group.shptm_display || '-';
        const jobordmernm = group.jobordmernm || '-';
        for (const loc of group.locations) {
            rows.push({
                delvedt,
                shptmDisplay,
                jobordmernm,
                shpctrnm: loc.shpctrnm || '-',
                jobordqun: Number(loc.jobordqun) || 0,
                addinfo02: loc.addinfo02 || ''
            });
        }
    }
    return rows;
}
