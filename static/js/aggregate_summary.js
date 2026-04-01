import { searchAggregateSummary, exportAggregateSummaryPdf, fetchMajorClassifications, fetchAggregateMiddleClassifications } from './api.js';
import { openPdfInIframe } from './pdf_generator.js';

let aggRows = [];
let aggMajorList = [];
let aggMiddleList = [];
let aggSelectedMajors = new Set();
let aggSelectedMiddles = new Set();

function formatYyyymmddToDisplay(yyyymmdd) {
    if (!yyyymmdd || yyyymmdd.length !== 8) return yyyymmdd || '-';
    return `${yyyymmdd.slice(0, 4)}-${yyyymmdd.slice(4, 6)}-${yyyymmdd.slice(6, 8)}`;
}

function getSelectedSummaryKeys() {
    const checked = document.querySelectorAll('.agg-item-checkbox:checked');
    const keys = [];
    checked.forEach(cb => {
        const index = Number(cb.dataset.index);
        const row = aggRows[index];
        if (row && row.key) {
            keys.push(row.key);
        }
    });
    return keys;
}

function updateAggSelectedSummary() {
    const majorLabel = document.getElementById('aggMajorSelectedLabel');
    const middleLabel = document.getElementById('aggMiddleSelectedLabel');

    if (majorLabel) {
        if (aggSelectedMajors.size === 0) {
            majorLabel.textContent = '未選択';
        } else if (aggSelectedMajors.size === aggMajorList.length) {
            majorLabel.textContent = 'すべて選択';
        } else {
            majorLabel.textContent = `${aggSelectedMajors.size}件選択`;
        }
    }

    if (middleLabel) {
        if (aggSelectedMiddles.size === 0) {
            middleLabel.textContent = '未選択';
        } else if (aggSelectedMiddles.size === aggMiddleList.length) {
            middleLabel.textContent = 'すべて選択';
        } else {
            middleLabel.textContent = `${aggSelectedMiddles.size}件選択`;
        }
    }
}

function buildAggMajorMiddlePanels() {
    const majorContainer = document.getElementById('aggMajorOptions');
    const middleContainer = document.getElementById('aggMiddleOptions');
    if (!majorContainer || !middleContainer) return;

    majorContainer.innerHTML = '';
    middleContainer.innerHTML = '';

    aggMajorList.forEach(m => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = m.code || '';
        if (aggSelectedMajors.has(cb.value)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) {
                if (cb.value) aggSelectedMajors.add(cb.value);
            } else {
                aggSelectedMajors.delete(cb.value);
            }
            updateAggSelectedSummary();
            // 大分類の選択変更に応じて中分類候補を更新
            loadAggMiddles();
        });
        const text = document.createElement('span');
        // ドロップダウンには名称のみ表示（コードは表示しない）
        text.textContent = m.name || '';
        label.appendChild(cb);
        label.appendChild(text);
        majorContainer.appendChild(label);
    });

    aggMiddleList.forEach(m => {
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = m.code || '';
        if (aggSelectedMiddles.has(cb.value)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) {
                if (cb.value) aggSelectedMiddles.add(cb.value);
                // 中分類選択時に対応する大分類も自動的に選択状態にする
                if (m.majorCode) {
                    aggSelectedMajors.add(m.majorCode);
                    // 画面上の大分類チェックボックスも同期
                    document
                        .querySelectorAll('#aggMajorOptions input[type="checkbox"]')
                        .forEach(majCb => {
                            if (majCb.value === m.majorCode) {
                                majCb.checked = true;
                            }
                        });
                }
            } else {
                aggSelectedMiddles.delete(cb.value);
            }
            updateAggSelectedSummary();
        });
        const text = document.createElement('span');
        // ドロップダウンには名称のみ表示（コードは表示しない）
        text.textContent = m.name || '';
        label.appendChild(cb);
        label.appendChild(text);
        middleContainer.appendChild(label);
    });

    updateAggSelectedSummary();
}

async function loadAggMiddles() {
    const middleContainer = document.getElementById('aggMiddleOptions');
    if (!middleContainer) return;

    const majorCodes = Array.from(aggSelectedMajors.values());
    if (majorCodes.length === 0) {
        // 大分類未選択時は全中分類を取得して表示
        try {
            const list = await fetchAggregateMiddleClassifications('');
            aggMiddleList = list || [];
            buildAggMajorMiddlePanels();
        } catch (e) {
            console.error('集計表 中分類マスタ取得エラー:', e);
        }
        return;
    }

    const firstCode = majorCodes[0];
    const major = aggMajorList.find(m => m.code === firstCode);
    if (!major) {
        aggMiddleList = [];
        buildAggMajorMiddlePanels();
        return;
    }

    try {
        const list = await fetchAggregateMiddleClassifications(major.code);
        aggMiddleList = list || [];
        buildAggMajorMiddlePanels();
    } catch (e) {
        console.error('集計表 中分類マスタ取得エラー:', e);
    }
}

document.addEventListener('DOMContentLoaded', () => {
    const searchBtn = document.getElementById('aggSearchBtn');
    const headerCheckbox = document.getElementById('aggHeaderCheckbox');
    const selectAllBtn = document.getElementById('aggSelectAllBtn');
    const deselectAllBtn = document.getElementById('aggDeselectAllBtn');
    const printBtn = document.getElementById('aggPrintBtn');
    const majorDisplay = document.getElementById('aggMajorDisplay');
    const middleDisplay = document.getElementById('aggMiddleDisplay');

    if (!searchBtn) return;

    // マスタ読み込み（大分類・中分類）
    (async () => {
        try {
            const majors = await fetchMajorClassifications();
            aggMajorList = majors || [];
            buildAggMajorMiddlePanels();
            // 初期表示時は大分類未選択なので、全中分類を取得して中分類ドロップダウンに表示する
            await loadAggMiddles();
        } catch (e) {
            console.error('集計表 大分類マスタ取得エラー:', e);
        }
    })();

    function closeAllAggPanels() {
        const majorPanel = document.getElementById('aggMajorOptions');
        const middlePanel = document.getElementById('aggMiddleOptions');
        if (majorPanel) majorPanel.style.display = 'none';
        if (middlePanel) middlePanel.style.display = 'none';
    }

    if (majorDisplay) {
        majorDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('aggMajorOptions');
            if (!panel) return;
            const isHidden = panel.style.display === 'none' || panel.style.display === '';
            closeAllAggPanels();
            panel.style.display = isHidden ? 'block' : 'none';
        });
    }

    if (middleDisplay) {
        middleDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('aggMiddleOptions');
            if (!panel) return;
            const isHidden = panel.style.display === 'none' || panel.style.display === '';
            closeAllAggPanels();
            panel.style.display = isHidden ? 'block' : 'none';
        });
    }

    document.addEventListener('click', (e) => {
        const dropdown = (e.target instanceof HTMLElement)
            ? e.target.closest('#screen-aggregate-summary .multi-select-dropdown')
            : null;
        if (!dropdown) {
            closeAllAggPanels();
        }
    });

    searchBtn.addEventListener('click', async () => {
        const shipDate = document.getElementById('aggShipDate').value;
        const itemCode = document.getElementById('aggItemCode').value || '';
        const majorCodes = Array.from(aggSelectedMajors.values());
        const middleCodes = Array.from(aggSelectedMiddles.values());

        try {
            let from = '';
            let to = '';
            if (shipDate) {
                let normalized = shipDate;
                if (normalized.includes('-')) {
                    normalized = normalized.replace(/-/g, '');
                }
                from = normalized;
                to = normalized;
            }

            const res = await searchAggregateSummary(from, to, itemCode, majorCodes, middleCodes);
            aggRows = res.rows || [];
            const section = document.getElementById('aggResultsSection');
            const printSection = document.getElementById('aggPrintSection');

            if (!aggRows.length) {
                alert('該当するデータが見つかりませんでした');
                section.style.display = 'none';
                printSection.style.display = 'none';
                return;
            }

            const countEl = document.getElementById('aggResultCount');
            const tbody = document.getElementById('aggResultsBody');
            countEl.textContent = `${aggRows.length}件`;
            tbody.innerHTML = '';

            aggRows.forEach((row, index) => {
                const tr = tbody.insertRow();
                const dateDisplay = row.shipDate || formatYyyymmddToDisplay(row.key?.shipDate || '');
                tr.innerHTML = `
                    <td><input type="checkbox" class="agg-item-checkbox" data-index="${index}"></td>
                    <td>${dateDisplay || '-'}</td>
                    <td>${row.majorClassificationName || '-'}</td>
                    <td>${row.middleClassificationName || '-'}</td>
                    <td class="col-num">${row.childItemCount ?? 0}</td>
                `;
                tr.style.cursor = 'pointer';
                tr.addEventListener('click', (e) => {
                    if (e.target.classList.contains('agg-item-checkbox')) return;
                    const cb = tr.querySelector('.agg-item-checkbox');
                    if (cb) cb.checked = !cb.checked;
                });
            });

            section.style.display = 'block';
            printSection.style.display = 'block';
            headerCheckbox.checked = false;
        } catch (e) {
            alert('検索に失敗しました: ' + e.message);
            console.error(e);
        }
    });

    headerCheckbox?.addEventListener('change', (e) => {
        const checked = e.target.checked;
        document.querySelectorAll('.agg-item-checkbox').forEach(cb => {
            cb.checked = checked;
        });
    });

    selectAllBtn?.addEventListener('click', () => {
        document.querySelectorAll('.agg-item-checkbox').forEach(cb => { cb.checked = true; });
        if (headerCheckbox) headerCheckbox.checked = true;
    });

    deselectAllBtn?.addEventListener('click', () => {
        document.querySelectorAll('.agg-item-checkbox').forEach(cb => { cb.checked = false; });
        if (headerCheckbox) headerCheckbox.checked = false;
    });

    printBtn?.addEventListener('click', async () => {
        const keys = getSelectedSummaryKeys();
        if (!keys.length) {
            alert('印刷するグループを選択してください');
            return;
        }

        const shipDate = document.getElementById('aggShipDate').value;
        const fromDate = shipDate || '';
        const toDate = shipDate || '';
        const itemCode = document.getElementById('aggItemCode').value || '';
        const majorCodes = Array.from(aggSelectedMajors.values());
        const middleCodes = Array.from(aggSelectedMiddles.values());

        const filter = {
            fromDate,
            toDate,
            itemCode,
            majorClass: majorCodes,
            middleClass: middleCodes
        };

        try {
            const blob = await exportAggregateSummaryPdf(filter, keys);
            openPdfInIframe(blob, '集計表 PDF 印刷');
        } catch (e) {
            alert('PDF出力に失敗しました: ' + e.message);
            console.error(e);
        }
    });
});

