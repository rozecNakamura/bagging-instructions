/**
 * 仕分け照会：検索・ピボット一覧
 */
import { fetchSortingInquirySlots, searchSortingInquiry } from './api.js';

let siSlotList = [];
const siSelectedSlotCodes = new Set();

function updateSiSlotLabel() {
    const slotLabel = document.getElementById('siSlotSelectedLabel');
    if (!slotLabel) return;
    if (siSelectedSlotCodes.size === 0) {
        slotLabel.textContent = '未選択';
    } else if (siSelectedSlotCodes.size === siSlotList.length && siSlotList.length > 0) {
        slotLabel.textContent = 'すべて選択';
    } else {
        slotLabel.textContent = `${siSelectedSlotCodes.size}件選択`;
    }
}

function buildSiSlotPanel() {
    const slotContainer = document.getElementById('siSlotOptions');
    if (!slotContainer) return;
    slotContainer.innerHTML = '';
    siSlotList.forEach(s => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = s.code || '';
        if (siSelectedSlotCodes.has(s.code)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) {
                if (s.code) siSelectedSlotCodes.add(s.code);
            } else {
                siSelectedSlotCodes.delete(s.code);
            }
            updateSiSlotLabel();
        });
        const text = document.createElement('span');
        text.textContent = s.name || s.code || '';
        label.appendChild(cb);
        label.appendChild(text);
        slotContainer.appendChild(label);
    });
    updateSiSlotLabel();
}

/** @param {unknown} v */
function formatSortingInquiryQty(v) {
    if (v == null || v === '') return '';
    const n = Number(v);
    if (Number.isNaN(n)) return String(v);
    if (Number.isInteger(n)) return String(n);
    const t = String(n);
    return t.replace(/(\.\d*?)0+$/, '$1').replace(/\.$/, '');
}

function displaySortingInquiryResults(data) {
    const section = document.getElementById('siResultsSection');
    const exportSection = document.getElementById('siExportSection');
    const countEl = document.getElementById('siResultCount');
    const thead = document.getElementById('siResultsThead');
    const tbody = document.getElementById('siResultsBody');

    if (!section || !exportSection || !countEl || !thead || !tbody) return;

    const rows = data?.rows || [];
    const storeKeys = data?.storeKeys || [];

    if (rows.length === 0 || storeKeys.length === 0) {
        alert('該当するデータが見つかりませんでした');
        section.style.display = 'none';
        exportSection.style.display = 'none';
        return;
    }

    countEl.textContent = `${rows.length}件`;
    const storeHeaderCodes = data.storeHeaderCodes || {};
    const storeHeaderDeliveryCodes = data.storeHeaderDeliveryCodes || {};
    const storeHeaderDeliveryNames = data.storeHeaderDeliveryNames || {};

    /** 店舗列の 3 段ヘッダー（1:納入場所コード 2:得意先コード 3:得意先名）。Excel の 4 行目列見出しは納入場所表示。 */
    const headerStackRowCount = 3;

    thead.innerHTML = '';

    const tr1 = thead.insertRow();
    ['品目コード', '品目名称', '適用'].forEach((label) => {
        const th = document.createElement('th');
        th.rowSpan = headerStackRowCount;
        th.textContent = label;
        tr1.appendChild(th);
    });
    storeKeys.forEach((key) => {
        const th = document.createElement('th');
        th.textContent = storeHeaderCodes[key] || storeHeaderDeliveryCodes[key] || key;
        tr1.appendChild(th);
    });
    const thSum = document.createElement('th');
    thSum.rowSpan = headerStackRowCount;
    thSum.textContent = '合計';
    tr1.appendChild(thSum);

    const tr2 = thead.insertRow();
    storeKeys.forEach((key) => {
        const th = document.createElement('th');
        th.textContent = storeHeaderDeliveryCodes[key] || '';
        tr2.appendChild(th);
    });

    const tr3 = thead.insertRow();
    storeKeys.forEach((key) => {
        const th = document.createElement('th');
        th.textContent = storeHeaderDeliveryNames[key] || '';
        tr3.appendChild(th);
    });

    tbody.innerHTML = '';
    rows.forEach((row) => {
        const tr = tbody.insertRow();
        tr.insertCell().textContent = row.itemCode || '-';
        tr.insertCell().textContent = row.itemName || '-';
        tr.insertCell().textContent = row.foodType || '-';
        const qty = row.quantitiesByStore || {};
        let lineSum = 0;
        storeKeys.forEach(key => {
            const v = qty[key];
            const td = tr.insertCell();
            if (v != null && v !== 0) {
                const n = Number(v);
                if (!Number.isNaN(n)) lineSum += n;
                td.textContent = formatSortingInquiryQty(v);
            } else {
                td.textContent = '';
            }
        });
        const sumTd = tr.insertCell();
        sumTd.textContent = lineSum !== 0 ? formatSortingInquiryQty(lineSum) : '';
    });

    section.style.display = 'block';
    exportSection.style.display = 'block';
}

/** @returns {{ delvedt: string, slotCodes: string[] }} */
export function getSortingInquiryExportParams() {
    const eatingDate = document.getElementById('siEatingDate')?.value || '';
    let delvedt = eatingDate;
    if (eatingDate && eatingDate.includes('-')) {
        delvedt = eatingDate.replace(/-/g, '');
    }
    return {
        delvedt,
        slotCodes: Array.from(siSelectedSlotCodes).filter(s => s)
    };
}

document.addEventListener('DOMContentLoaded', () => {
    const searchBtn = document.getElementById('siSearchBtn');
    const slotDisplay = document.getElementById('siSlotDisplay');

    if (!searchBtn) return;

    (async () => {
        try {
            siSlotList = await fetchSortingInquirySlots() || [];
            buildSiSlotPanel();
        } catch (e) {
            console.error('仕分け照会 便マスタ取得エラー:', e);
        }
    })();

    function closeSiPanel() {
        const panel = document.getElementById('siSlotOptions');
        if (panel) panel.style.display = 'none';
    }

    if (slotDisplay) {
        slotDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('siSlotOptions');
            if (!panel) return;
            const isHidden = panel.style.display === 'none' || panel.style.display === '';
            closeSiPanel();
            panel.style.display = isHidden ? 'block' : 'none';
        });
    }

    document.addEventListener('click', (e) => {
        const dropdown = (e.target instanceof HTMLElement)
            ? e.target.closest('#screen-sorting-inquiry .multi-select-dropdown')
            : null;
        if (!dropdown) closeSiPanel();
    });

    searchBtn.addEventListener('click', async () => {
        const eatingDate = document.getElementById('siEatingDate')?.value || '';
        if (!eatingDate) {
            alert('喫食日を入力してください');
            return;
        }
        const slotCodes = Array.from(siSelectedSlotCodes);
        try {
            const res = await searchSortingInquiry(eatingDate, slotCodes);
            displaySortingInquiryResults(res);
        } catch (e) {
            alert('検索に失敗しました: ' + e.message);
            console.error(e);
        }
    });
});
