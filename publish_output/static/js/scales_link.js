/**
 * 計量器連携：マスタ CSV ダウンロード・オーダー検索・ORDER.csv
 */
import { searchScalesLinkOrders } from './api.js';

const API_BASE =
    typeof window !== 'undefined' && typeof window.__API_BASE__ === 'string'
        ? window.__API_BASE__ + '/scales-link'
        : '/api/scales-link';

function getDateInputs() {
    const fromEl = document.getElementById('slReleaseDateFrom');
    const toEl = document.getElementById('slReleaseDateTo');
    return {
        from: fromEl?.value || '',
        to: toEl?.value || ''
    };
}

function buildOrderQueryString() {
    const { from, to } = getDateInputs();
    const params = new URLSearchParams();
    if (from) params.set('releaseDateFrom', from);
    if (to) params.set('releaseDateTo', to);
    return params.toString();
}

function compareYmd(a, b) {
    if (!a || !b) return 0;
    return a.localeCompare(b);
}

function setExportEnabled(enabled) {
    const btn = document.getElementById('slExportOrderBtn');
    if (btn) btn.disabled = !enabled;
}

function invalidateSearchState() {
    setExportEnabled(false);
}

document.addEventListener('DOMContentLoaded', () => {
    document.getElementById('slItemMasterBtn')?.addEventListener('click', () => {
        window.location.href = `${API_BASE}/master/item`;
    });
    document.getElementById('slMbomMasterBtn')?.addEventListener('click', () => {
        window.location.href = `${API_BASE}/master/mbom`;
    });

    document.getElementById('slReleaseDateFrom')?.addEventListener('change', invalidateSearchState);
    document.getElementById('slReleaseDateTo')?.addEventListener('change', invalidateSearchState);

    document.getElementById('slSearchBtn')?.addEventListener('click', async () => {
        const { from, to } = getDateInputs();
        if (from && to && compareYmd(from, to) > 0) {
            alert('着手日の開始日は終了日以前を指定してください');
            return;
        }
        const section = document.getElementById('slResultsSection');
        const tbody = document.getElementById('slResultsBody');
        const countEl = document.getElementById('slResultCount');
        if (!tbody || !countEl || !section) return;
        try {
            const data = await searchScalesLinkOrders(from || null, to || null);
            const orders = data.orders || [];
            countEl.textContent = `${data.totalCount ?? orders.length}件`;
            tbody.replaceChildren();
            for (const row of orders) {
                const tr = document.createElement('tr');
                const cells = [
                    row.ordertableid,
                    row.itemcode,
                    row.addinfo06,
                    row.releasedate != null ? String(row.releasedate) : '',
                    row.workcentercode,
                    row.qty != null ? String(row.qty) : ''
                ];
                for (const v of cells) {
                    const td = document.createElement('td');
                    td.textContent = v != null ? String(v) : '';
                    tr.appendChild(td);
                }
                tbody.appendChild(tr);
            }
            section.style.display = '';
            setExportEnabled(true);
        } catch (e) {
            alert(e.message || String(e));
            console.error(e);
        }
    });

    document.getElementById('slExportOrderBtn')?.addEventListener('click', () => {
        const qs = buildOrderQueryString();
        const url = qs ? `${API_BASE}/orders/export?${qs}` : `${API_BASE}/orders/export`;
        window.location.href = url;
    });
});
