/**
 * 弁当箱盛り付け指示書（ご飯）：検索・結果表示・選択（喫食日・喫食時間・品目でグループ化表示）
 */
import { searchBento } from './api.js';

/** 検索結果グループ一覧（喫食日・喫食時間・品目でまとめた単位） */
let bentoSearchGroups = [];

document.getElementById('bentoSearchBtn').addEventListener('click', async () => {
    const eatingDate = document.getElementById('bentoEatingDate').value;
    const itemCode = document.getElementById('bentoItemCode').value?.trim() || '';

    if (!eatingDate) {
        alert('喫食日を入力してください');
        return;
    }

    try {
        const response = await searchBento(eatingDate, itemCode || undefined);
        bentoSearchGroups = response.groups || [];
        displayBentoResults(bentoSearchGroups);
    } catch (error) {
        alert('検索に失敗しました: ' + error.message);
    }
});

function displayBentoResults(groups) {
    const section = document.getElementById('bentoResultsSection');
    const printSection = document.getElementById('bentoPrintSection');
    const countEl = document.getElementById('bentoResultCount');
    const tbody = document.getElementById('bentoResultsBody');

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
            <td><input type="checkbox" class="bento-item-checkbox" data-group-index="${index}"></td>
            <td>${formatDate(group.delvedt) || '-'}</td>
            <td>${group.shptm_display || '-'}</td>
            <td>${group.itemcd || '-'}</td>
            <td>${group.jobordmernm || '-'}</td>
            <td>${locationCount}</td>
        `;
        row.style.cursor = 'pointer';
        row.addEventListener('click', (e) => {
            if (e.target.classList.contains('bento-item-checkbox')) return;
            const cb = row.querySelector('.bento-item-checkbox');
            if (cb) cb.checked = !cb.checked;
        });
    });

    section.style.display = 'block';
    printSection.style.display = 'flex';
    document.getElementById('bentoHeaderCheckbox').checked = false;
}

function formatDate(yyyymmdd) {
    if (!yyyymmdd || yyyymmdd.length !== 8) return yyyymmdd;
    return `${yyyymmdd.slice(0, 4)}-${yyyymmdd.slice(4, 6)}-${yyyymmdd.slice(6, 8)}`;
}

document.getElementById('bentoHeaderCheckbox').addEventListener('change', (e) => {
    document.querySelectorAll('.bento-item-checkbox').forEach(cb => { cb.checked = e.target.checked; });
});

document.getElementById('bentoSelectAllBtn').addEventListener('click', () => {
    document.querySelectorAll('.bento-item-checkbox').forEach(cb => { cb.checked = true; });
    document.getElementById('bentoHeaderCheckbox').checked = true;
});

document.getElementById('bentoDeselectAllBtn').addEventListener('click', () => {
    document.querySelectorAll('.bento-item-checkbox').forEach(cb => { cb.checked = false; });
    document.getElementById('bentoHeaderCheckbox').checked = false;
});

/**
 * 選択されたグループを、PDF用に「納入場所ごと1行」に展開して返す
 * @returns {{ delvedt: string, shptmDisplay: string, jobordmernm: string, jobordqun: number, quantity: number, addinfo02: string }[]}
 */
export function getSelectedBentoRows() {
    const checked = document.querySelectorAll('.bento-item-checkbox:checked');
    const indices = new Set(Array.from(checked).map(cb => parseInt(cb.dataset.groupIndex, 10)));
    const rows = [];
    for (const i of indices) {
        const group = bentoSearchGroups[i];
        if (!group || !group.locations || group.locations.length === 0) continue;
        const delvedt = formatDate(group.delvedt) || '-';
        const shptmDisplay = group.shptm_display || '-';
        const jobordmernm = group.jobordmernm || '-';
        for (const loc of group.locations) {
            rows.push({
                delvedt,
                shptmDisplay,
                jobordmernm,
                jobordqun: Number(loc.jobordqun) || 0,
                quantity: Number(loc.quantity) || 0,
                addinfo02: loc.addinfo02 ?? ''
            });
        }
    }
    return rows;
}
