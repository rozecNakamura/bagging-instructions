/**
 * 予定食数：検索・Excel出力
 */
import { fetchSortingInquirySlots } from './api.js';

let ysSlotList = [];
const ysSelectedSlotCodes = new Set();

function getApiBase() {
    return (typeof window !== 'undefined' && typeof window.__API_BASE__ === 'string')
        ? window.__API_BASE__
        : '/api';
}

function updateYsSlotLabel() {
    const label = document.getElementById('ysSlotSelectedLabel');
    if (!label) return;
    if (ysSelectedSlotCodes.size === 0) {
        label.textContent = '未選択';
    } else if (ysSelectedSlotCodes.size === ysSlotList.length && ysSlotList.length > 0) {
        label.textContent = 'すべて選択';
    } else {
        label.textContent = `${ysSelectedSlotCodes.size}件選択`;
    }
}

function buildYsSlotPanel() {
    const container = document.getElementById('ysSlotOptions');
    if (!container) return;
    container.innerHTML = '';
    ysSlotList.forEach(s => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = s.code || '';
        if (ysSelectedSlotCodes.has(cb.value)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) ysSelectedSlotCodes.add(cb.value);
            else ysSelectedSlotCodes.delete(cb.value);
            updateYsSlotLabel();
        });
        const text = document.createElement('span');
        text.textContent = s.name || s.code || '';
        label.appendChild(cb);
        label.appendChild(text);
        container.appendChild(label);
    });
    updateYsSlotLabel();
}

function getYsDelvedt() {
    const v = document.getElementById('ysEatingDate')?.value?.trim() ?? '';
    return v.includes('-') ? v.replace(/-/g, '') : v;
}

function getYsSlotCodes() {
    return Array.from(ysSelectedSlotCodes).filter(c => c);
}

function buildSlotParams(delvedt, slotCodes) {
    const params = new URLSearchParams({ delvedt });
    (slotCodes || []).forEach(c => { if (c && String(c).trim()) params.append('slot_code', String(c).trim()); });
    return params;
}

async function searchYoteiShokusu(delvedt, slotCodes) {
    const params = buildSlotParams(delvedt, slotCodes);
    const res = await fetch(`${getApiBase()}/yotei-shokusu/search?${params}`);
    if (!res.ok) {
        const t = await res.text();
        let msg = `検索エラー: ${res.status}`;
        try { const j = JSON.parse(t); if (j.detail) msg += ' - ' + j.detail; } catch (_) { if (t) msg += ' - ' + t; }
        throw new Error(msg);
    }
    return await res.json();
}

function formatQty(v) {
    if (v == null || v === 0) return '';
    const n = Number(v);
    if (Number.isNaN(n) || n === 0) return '';
    return Number.isInteger(n) ? String(n) : String(n).replace(/(\.\d*?)0+$/, '$1').replace(/\.$/, '');
}

function displayYsResults(data) {
    const section = document.getElementById('ysResultsSection');
    const exportSection = document.getElementById('ysExportSection');
    if (!section || !exportSection) return;

    const g1Stores = data?.group1Stores ?? [];
    const g2Stores = data?.group2Stores ?? [];
    const g1Columns = data?.group1Columns ?? [];
    const g2Columns = data?.group2Columns ?? [];

    if (g1Stores.length === 0 && g2Stores.length === 0) {
        alert('該当するデータが見つかりませんでした');
        section.style.display = 'none';
        exportSection.style.display = 'none';
        return;
    }

    // グループ1テーブル
    const g1Section = document.getElementById('ysGroup1Section');
    const g1Table = document.getElementById('ysGroup1Table');
    if (g1Table && g1Stores.length > 0) {
        const thead = g1Table.querySelector('thead');
        const tbody = g1Table.querySelector('tbody');
        thead.innerHTML = '';
        tbody.innerHTML = '';

        const hRow = document.createElement('tr');
        ['施設名', '区分', ...g1Columns, '合計'].forEach(h => {
            const th = document.createElement('th');
            th.textContent = h;
            hRow.appendChild(th);
        });
        thead.appendChild(hRow);

        g1Stores.forEach(store => {
            store.rows.forEach((row, ri) => {
                const tr = document.createElement('tr');
                if (ri === 0) {
                    const td = document.createElement('td');
                    td.textContent = store.locationName || store.locationCode;
                    td.rowSpan = store.rows.length;
                    tr.appendChild(td);
                }
                const secTd = document.createElement('td');
                secTd.textContent = row.sectionLabel || '';
                tr.appendChild(secTd);

                let rowTotal = 0;
                g1Columns.forEach(col => {
                    const td = document.createElement('td');
                    td.className = 'col-num';
                    const q = row.quantities?.[col] ?? 0;
                    rowTotal += Number(q) || 0;
                    td.textContent = formatQty(q);
                    tr.appendChild(td);
                });
                const totalTd = document.createElement('td');
                totalTd.className = 'col-num';
                totalTd.textContent = formatQty(Math.round(rowTotal * 10000) / 10000);
                tr.appendChild(totalTd);
                tbody.appendChild(tr);
            });
        });
        if (g1Section) g1Section.style.display = 'block';
    } else {
        if (g1Section) g1Section.style.display = 'none';
    }

    // グループ2テーブル
    const g2Section = document.getElementById('ysGroup2Section');
    const g2Table = document.getElementById('ysGroup2Table');
    if (g2Table && g2Stores.length > 0) {
        const thead = g2Table.querySelector('thead');
        const tbody = g2Table.querySelector('tbody');
        thead.innerHTML = '';
        tbody.innerHTML = '';

        const hRow = document.createElement('tr');
        ['施設名', ...g2Columns, '合計'].forEach(h => {
            const th = document.createElement('th');
            th.textContent = h;
            hRow.appendChild(th);
        });
        thead.appendChild(hRow);

        g2Stores.forEach(store => {
            const row = store.rows[0];
            const tr = document.createElement('tr');
            const nameTd = document.createElement('td');
            nameTd.textContent = store.locationName || store.locationCode;
            tr.appendChild(nameTd);

            let rowTotal = 0;
            g2Columns.forEach(col => {
                const td = document.createElement('td');
                td.className = 'col-num';
                const q = row?.quantities?.[col] ?? 0;
                rowTotal += Number(q) || 0;
                td.textContent = formatQty(q);
                tr.appendChild(td);
            });
            const totalTd = document.createElement('td');
            totalTd.className = 'col-num';
            totalTd.textContent = formatQty(Math.round(rowTotal * 10000) / 10000);
            tr.appendChild(totalTd);
            tbody.appendChild(tr);
        });
        if (g2Section) g2Section.style.display = 'block';
    } else {
        if (g2Section) g2Section.style.display = 'none';
    }

    section.style.display = 'block';
    exportSection.style.display = 'flex';
}

document.addEventListener('DOMContentLoaded', () => {
    (async () => {
        try {
            const slots = await fetchSortingInquirySlots();
            ysSlotList = slots || [];
            buildYsSlotPanel();
        } catch (e) {
            console.error('予定食数 便マスタ取得エラー:', e);
        }
    })();

    const slotDisplay = document.getElementById('ysSlotDisplay');
    if (slotDisplay) {
        slotDisplay.addEventListener('click', e => {
            e.stopPropagation();
            const panel = document.getElementById('ysSlotOptions');
            if (!panel) return;
            const hidden = panel.style.display === 'none' || panel.style.display === '';
            panel.style.display = hidden ? 'block' : 'none';
        });
    }

    document.addEventListener('click', e => {
        const inDropdown = (e.target instanceof HTMLElement)
            ? e.target.closest('#screen-yotei-shokusu .multi-select-dropdown')
            : null;
        if (!inDropdown) {
            const panel = document.getElementById('ysSlotOptions');
            if (panel) panel.style.display = 'none';
        }
    });

    document.getElementById('ysSearchBtn')?.addEventListener('click', async () => {
        const delvedt = getYsDelvedt();
        if (!delvedt) { alert('喫食日を入力してください'); return; }
        try {
            const data = await searchYoteiShokusu(delvedt, getYsSlotCodes());
            displayYsResults(data);
        } catch (e) {
            alert('検索に失敗しました: ' + e.message);
            console.error(e);
        }
    });

    document.getElementById('ysExcelBtn')?.addEventListener('click', () => {
        const delvedt = getYsDelvedt();
        if (!delvedt) { alert('喫食日を入力してください'); return; }
        const params = buildSlotParams(delvedt, getYsSlotCodes());
        const url = `${getApiBase()}/yotei-shokusu/export?${params}`;
        const a = document.createElement('a');
        a.href = url;
        a.download = `5_予定食数_${delvedt}.xlsx`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
    });
});
