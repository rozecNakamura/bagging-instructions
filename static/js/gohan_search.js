/**
 * ご飯盛り付け指示書：検索・結果表示・選択（喫食日・喫食時間・品目でグループ化表示）
 */
import { searchGohan } from './api.js';

/** 検索結果グループ一覧（喫食日・喫食時間・品目でまとめた単位） */
let gohanSearchGroups = [];

document.getElementById('gohanSearchBtn').addEventListener('click', async () => {
    const eatingDate = document.getElementById('gohanEatingDate').value;
    const itemCode = document.getElementById('gohanItemCode').value?.trim() || '';
    const addinfo08Type = document.querySelector('input[name="gohanAddinfo08Type"]:checked')?.value ?? '';

    if (!eatingDate) {
        alert('喫食日を入力してください');
        return;
    }

    try {
        const response = await searchGohan(eatingDate, itemCode || undefined, addinfo08Type || undefined);
        gohanSearchGroups = response.groups || [];
        displayGohanResults(gohanSearchGroups);
    } catch (error) {
        alert('検索に失敗しました: ' + error.message);
    }
});

function displayGohanResults(groups) {
    const section = document.getElementById('gohanResultsSection');
    const printSection = document.getElementById('gohanPrintSection');
    const countEl = document.getElementById('gohanResultCount');
    const tbody = document.getElementById('gohanResultsBody');

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
            <td><input type="checkbox" class="gohan-item-checkbox" data-group-index="${index}"></td>
            <td>${formatDate(group.delvedt) || '-'}</td>
            <td>${group.shptm_display || '-'}</td>
            <td>${group.itemcd || '-'}</td>
            <td>${group.jobordmernm || '-'}</td>
            <td>${locationCount}</td>
        `;
        row.style.cursor = 'pointer';
        row.addEventListener('click', (e) => {
            if (e.target.classList.contains('gohan-item-checkbox')) return;
            const cb = row.querySelector('.gohan-item-checkbox');
            if (cb) cb.checked = !cb.checked;
        });
    });

    section.style.display = 'block';
    printSection.style.display = 'flex';
    document.getElementById('gohanHeaderCheckbox').checked = false;
}

function formatDate(yyyymmdd) {
    if (!yyyymmdd || yyyymmdd.length !== 8) return yyyymmdd;
    return `${yyyymmdd.slice(0, 4)}-${yyyymmdd.slice(4, 6)}-${yyyymmdd.slice(6, 8)}`;
}

document.getElementById('gohanHeaderCheckbox').addEventListener('change', (e) => {
    document.querySelectorAll('.gohan-item-checkbox').forEach(cb => { cb.checked = e.target.checked; });
});

document.getElementById('gohanSelectAllBtn').addEventListener('click', () => {
    document.querySelectorAll('.gohan-item-checkbox').forEach(cb => { cb.checked = true; });
    document.getElementById('gohanHeaderCheckbox').checked = true;
});

document.getElementById('gohanDeselectAllBtn').addEventListener('click', () => {
    document.querySelectorAll('.gohan-item-checkbox').forEach(cb => { cb.checked = false; });
    document.getElementById('gohanHeaderCheckbox').checked = false;
});

/**
 * 選択されたグループを、PDF用に「納入場所ごと1行」に展開して返す
 * @returns {{ delvedt: string, jobordmernm: string, jobordqun: number, quantity: number, addinfo01: string, addinfo08: string, addinfo05: string, shpctrnm: string }[]}
 */
export function getSelectedGohanRows() {
    const checked = document.querySelectorAll('.gohan-item-checkbox:checked');
    const indices = new Set(Array.from(checked).map(cb => parseInt(cb.dataset.groupIndex, 10)));
    const rows = [];
    for (const i of indices) {
        const group = gohanSearchGroups[i];
        if (!group || !group.locations || group.locations.length === 0) continue;
        const delvedt = formatDate(group.delvedt) || '-';
        const jobordmernm = group.jobordmernm || '-';
        const addinfo05 = group.addinfo05 ?? '';
        const itemcd = group.itemcd ?? '';
        for (const loc of group.locations) {
            rows.push({
                delvedt,
                jobordmernm,
                itemcd,
                cuscd: loc.cuscd ?? '',
                shpctrcd: loc.shpctrcd ?? '',
                jobordqun: Number(loc.jobordqun) || 0,
                quantity: Number(loc.quantity) || 0,
                addinfo01: loc.addinfo01 ?? '',
                addinfo08: loc.addinfo08 ?? '',
                addinfo05,
                shpctrnm: loc.shpctrnm ?? ''
            });
        }
    }
    return rows;
}
