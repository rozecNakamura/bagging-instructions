/**
 * 予定食数：検索・Excel出力
 */

function getApiBase() {
    return (typeof window !== 'undefined' && typeof window.__API_BASE__ === 'string')
        ? window.__API_BASE__
        : '/api';
}

function getYsDelvedt() {
    const v = document.getElementById('ysEatingDate')?.value?.trim() ?? '';
    return v.includes('-') ? v.replace(/-/g, '') : v;
}

/** 選択中の時間帯コード（1=朝/2=昼/3=夕）。未選択は null。 */
function getYsMealTime() {
    return document.querySelector('input[name="ysMealTime"]:checked')?.value ?? null;
}

function getYsCustomerGroups() {
    const groups = [];
    if (document.getElementById('ysHospital')?.checked) groups.push('hospital');
    if (document.getElementById('ysDaycare')?.checked) groups.push('daycare');
    if (document.getElementById('ysHome')?.checked) groups.push('home');
    return groups;
}

function buildSearchParams(delvedt, mealTime, customerGroups) {
    const params = new URLSearchParams({ delvedt });
    if (mealTime) params.append('meal_time', mealTime);
    (customerGroups || []).forEach(g => { if (g) params.append('customer_group', g); });
    return params;
}

async function searchYoteiShokusu(delvedt, mealTime, customerGroups) {
    const params = buildSearchParams(delvedt, mealTime, customerGroups);
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
    document.getElementById('ysSearchBtn')?.addEventListener('click', async () => {
        const delvedt = getYsDelvedt();
        if (!delvedt) { alert('喫食日を入力してください'); return; }
        const mealTime = getYsMealTime();
        if (!mealTime) { alert('時間帯を選択してください'); return; }
        const groups = getYsCustomerGroups();
        if (groups.length === 0) { alert('得意先を1つ以上選択してください'); return; }
        try {
            const data = await searchYoteiShokusu(delvedt, mealTime, groups);
            displayYsResults(data);
        } catch (e) {
            alert('検索に失敗しました: ' + e.message);
            console.error(e);
        }
    });

    document.getElementById('ysExcelBtn')?.addEventListener('click', () => {
        const delvedt = getYsDelvedt();
        if (!delvedt) { alert('喫食日を入力してください'); return; }
        const mealTime = getYsMealTime();
        if (!mealTime) { alert('時間帯を選択してください'); return; }
        const params = buildSearchParams(delvedt, mealTime, getYsCustomerGroups());
        const url = `${getApiBase()}/yotei-shokusu/export?${params}`;
        const mealLabels = { '1': '朝', '2': '昼', '3': '夕' };
        const timeLabel = mealTime ? `_${mealLabels[mealTime] ?? mealTime}` : '';
        const a = document.createElement('a');
        a.href = url;
        a.download = `5_予定食数_${delvedt}${timeLabel}.xlsx`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
    });
});
