/**
 * 検索・チェックボックス管理
 */

import { searchOrders } from './api.js';

let searchResults = [];

// 検索ボタンイベント
document.getElementById('searchBtn').addEventListener('click', async () => {
    const productionDate = document.getElementById('productionDate').value;
    const productCode = document.getElementById('productCode').value;
    
    if (!productionDate || !productCode) {
        alert('製造日と品目コードを入力してください');
        return;
    }
    
    try {
        const response = await searchOrders(productionDate, productCode);
        searchResults = response.items;
        displayResults(searchResults);
    } catch (error) {
        alert('検索に失敗しました: ' + error.message);
    }
});

// 検索結果を表示
function displayResults(items) {
    const resultsSection = document.getElementById('resultsSection');
    const printSection = document.getElementById('printSection');
    const resultCount = document.getElementById('resultCount');
    const tbody = document.getElementById('resultsBody');
    
    if (items.length === 0) {
        alert('該当するデータが見つかりませんでした');
        resultsSection.style.display = 'none';
        printSection.style.display = 'none';
        return;
    }
    
    resultCount.textContent = `${items.length}件`;
    tbody.innerHTML = '';
    
    items.forEach(item => {
        const row = tbody.insertRow();
        row.innerHTML = `
            <td><input type="checkbox" class="item-checkbox" data-id="${item.id}"></td>
            <td>${item.production_date}</td>
            <td>${item.eating_date}</td>
            <td>${item.eating_time}</td>
            <td>${item.customer_code}</td>
            <td>${item.facility_code}</td>
            <td>${item.facility_name || '-'}</td>
            <td>${item.product_code}</td>
            <td>${item.product_name || '-'}</td>
            <td>${item.order_quantity}</td>
        `;
    });
    
    resultsSection.style.display = 'block';
    printSection.style.display = 'block';
}

// ヘッダーチェックボックス（全選択・全解除）
document.getElementById('headerCheckbox').addEventListener('change', (e) => {
    const checkboxes = document.querySelectorAll('.item-checkbox');
    checkboxes.forEach(cb => cb.checked = e.target.checked);
});

// 全選択ボタン
document.getElementById('selectAllBtn').addEventListener('click', () => {
    const checkboxes = document.querySelectorAll('.item-checkbox');
    checkboxes.forEach(cb => cb.checked = true);
    document.getElementById('headerCheckbox').checked = true;
});

// 全解除ボタン
document.getElementById('deselectAllBtn').addEventListener('click', () => {
    const checkboxes = document.querySelectorAll('.item-checkbox');
    checkboxes.forEach(cb => cb.checked = false);
    document.getElementById('headerCheckbox').checked = false;
});

// 選択されたIDを取得
export function getSelectedIds() {
    const checkboxes = document.querySelectorAll('.item-checkbox:checked');
    return Array.from(checkboxes).map(cb => parseInt(cb.dataset.id));
}

