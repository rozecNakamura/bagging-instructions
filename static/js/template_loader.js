/**
 * テンプレート読み込み・データ注入モジュール
 */

/**
 * CSSをスコープ化する
 * テンプレートのスタイルがメインページのスタイルを上書きしないように、
 * すべてのセレクタに #print-preview-container プレフィックスを追加
 * @param {string} css - 元のCSS
 * @returns {string} スコープ化されたCSS
 */
function scopeStylesheet(css) {
    let result = '';
    let depth = 0;
    let inAtRule = false;
    let atRuleType = '';
    
    // CSSを行ごとに処理
    const lines = css.split('\n');
    
    for (let i = 0; i < lines.length; i++) {
        let line = lines[i];
        const trimmed = line.trim();
        
        // @ルールの開始を検出
        if (trimmed.startsWith('@page') || trimmed.startsWith('@font-face')) {
            // これらのルールはスコープ化しない
            result += line + '\n';
            if (!trimmed.includes('{')) continue;
            if (trimmed.includes('}')) continue;
            inAtRule = true;
            atRuleType = 'no-scope';
            depth++;
            continue;
        } else if (trimmed.startsWith('@media') || trimmed.startsWith('@supports')) {
            // メディアクエリ内のセレクタはスコープ化する
            result += line + '\n';
            if (!trimmed.includes('{')) continue;
            inAtRule = true;
            atRuleType = 'scope';
            depth++;
            continue;
        }
        
        // 括弧のカウント
        const openBraces = (line.match(/{/g) || []).length;
        const closeBraces = (line.match(/}/g) || []).length;
        
        // @ルール内でスコープ化不要な場合
        if (inAtRule && atRuleType === 'no-scope') {
            result += line + '\n';
            depth += openBraces - closeBraces;
            if (depth === 0) {
                inAtRule = false;
                atRuleType = '';
            }
            continue;
        }
        
        // コメント行はそのまま
        if (trimmed.startsWith('/*') || trimmed.startsWith('*') || trimmed.startsWith('*/')) {
            result += line + '\n';
            continue;
        }
        
        // 空行はそのまま
        if (trimmed === '') {
            result += line + '\n';
            continue;
        }
        
        // セレクタ行（{を含む）の処理
        if (line.includes('{') && !trimmed.startsWith('@')) {
            const parts = line.split('{');
            const selectorPart = parts[0];
            const rest = parts.slice(1).join('{');
            
            // セレクタをスコープ化
            const selectors = selectorPart.split(',').map(s => {
                const selector = s.trim();
                if (selector === '') return selector;
                
                // bodyセレクタは #print-preview-container に置き換え
                if (selector === 'body') {
                    return '#print-preview-container';
                } else if (selector.startsWith('body ')) {
                    return selector.replace(/^body/, '#print-preview-container');
                } else if (selector.startsWith('body.')) {
                    return selector.replace(/^body/, '#print-preview-container');
                } else if (selector.startsWith('body:')) {
                    return selector.replace(/^body/, '#print-preview-container');
                } else if (selector.startsWith('body>')) {
                    return selector.replace(/^body/, '#print-preview-container');
                } else {
                    // その他のセレクタには #print-preview-container をプレフィックス
                    return `#print-preview-container ${selector}`;
                }
            });
            
            result += selectors.join(', ') + ' {' + rest + '\n';
        } else {
            // プロパティ行や閉じ括弧はそのまま
            result += line + '\n';
        }
        
        depth += openBraces - closeBraces;
        if (depth === 0 && inAtRule) {
            inAtRule = false;
            atRuleType = '';
        }
    }
    
    return result;
}

/**
 * HTMLテンプレートを読み込む
 * @param {string} templatePath - テンプレートファイルパス
 * @returns {Promise<string>} HTMLテンプレート文字列
 */
export async function loadTemplate(templatePath) {
    try {
        const response = await fetch(templatePath);
        if (!response.ok) {
            throw new Error(`テンプレート読み込みエラー: ${response.status}`);
        }
        return await response.text();
    } catch (error) {
        console.error('テンプレート読み込み失敗:', error);
        throw error;
    }
}

/**
 * HTMLテンプレートにデータを注入する
 * @param {string} templateHtml - HTMLテンプレート文字列
 * @param {Object} data - 注入するデータ
 * @returns {HTMLElement} データが注入されたHTML要素
 */
export function injectData(templateHtml, data) {
    // DOMParserを使って正しくHTMLをパース
    const parser = new DOMParser();
    const doc = parser.parseFromString(templateHtml, 'text/html');
    
    // 現在の日時を設定
    const now = new Date();
    let printDate = `${now.getFullYear()}/${String(now.getMonth() + 1).padStart(2, '0')}/${String(now.getDate()).padStart(2, '0')} ${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`;
    
    // ページ番号がある場合は追加表示
    if (data.pageNumber && data.totalPages && data.totalPages > 1) {
        printDate += ` (${data.pageNumber}/${data.totalPages})`;
    }
    
    const printDateElement = doc.querySelector('#printDate');
    if (printDateElement) {
        printDateElement.textContent = printDate;
    }
    
    // ヘッダー情報を注入
    injectHeaderData(doc, data);
    
    // 工程・レシピ情報テーブル（右上）を注入
    injectIngredientsTableData(doc, data);
    
    // メインテーブルのヘッダー行を動的に設定
    injectMainTableHeaders(doc, data);
    
    // テーブルデータを注入（合計行も含む）
    injectTableData(doc, data);
    
    // スタイルタグを抽出
    const styles = doc.querySelectorAll('style');
    
    // body内のコンテンツを取得
    const bodyContent = doc.body.cloneNode(true);
    
    // ラッパーコンテナを作成してスタイルとコンテンツを追加
    const wrapper = document.createElement('div');
    wrapper.className = 'print-wrapper';
    
    // スタイルを追加（スコープ化）
    styles.forEach(style => {
        const scopedStyle = document.createElement('style');
        // すべてのセレクタに #print-preview-container プレフィックスを追加してスコープ化
        scopedStyle.textContent = scopeStylesheet(style.textContent);
        wrapper.appendChild(scopedStyle);
    });
    
    // bodyの内容を追加
    while (bodyContent.firstChild) {
        wrapper.appendChild(bodyContent.firstChild);
    }
    
    return wrapper;
}
/**
 * 殺菌温度（袋詰仕様：温度のみ表示）
 * @param {string} steritemprange - 殺菌温度範囲（例：95℃）
 * @returns {string}
 */
function formatSterilizationTemp(steritemprange) {
    if (!steritemprange) return '';
    return String(steritemprange);
}

/**
 * ヘッダー情報を注入（commonInfo対応版）
 * @param {HTMLElement} container - コンテナ要素
 * @param {Object} data - データオブジェクト
 */
function injectHeaderData(container, data) {
    // commonInfo があればそれを使用、なければ最初のitemを使用（後方互換性）
    const sourceData = data.commonInfo || (data.items && data.items.length > 0 ? data.items[0] : null);
    
    if (!sourceData) return;
    
    const item = sourceData.item;  // ITEM（品目マスタ）
    const shpctr = sourceData.shpctr;  // SHPCTR（納入場所マスタ）
    const routs = item?.routs || [];  // ROUT（工程手順マスタ）配列
    const mboms = sourceData.mboms || [];  // MBOM（レシピマスタ）配列
    const cusmcd = sourceData.cusmcd;  // CUSMCD（得意先品目変換マスタ）
    
    // 最初の工程とレシピ
    const firstRout = routs && routs.length > 0 ? routs[0] : null;
    const firstMbom = mboms && mboms.length > 0 ? mboms[0] : null;
    
    // 喫食日情報を更新
    const eatingDateElement = container.querySelector('#eatingDateInfo');
    if (eatingDateElement && sourceData) {
        const delvedt = sourceData.delvedt || '';
        const shptm = sourceData.shptm || '';
        let timeLabel = '';
        if (shptm && shptm.length >= 2) {
            const hour = parseInt(shptm.substr(0,2));
            if (hour < 10) timeLabel = '朝';
            else if (hour < 15) timeLabel = '昼';
            else timeLabel = '夕';
        }
        eatingDateElement.textContent = `喫食日:${delvedt} ${timeLabel}`;
    }
    
    const headerFields = {
        'productionDate': sourceData.prddt || '',
        'productDate': sourceData.delvedt || '',
        'kitCode': sourceData.itemcd || '',
        'kitName': sourceData.jobordmernm || '',
        'cookingType': '',
        'grade': (() => {
            const specParts = [
                item?.classification1_code ?? item?.spec1,
                item?.classification2_code ?? item?.spec2,
                item?.classification3_code ?? item?.spec3,
            ].filter(Boolean);
            if (specParts.length) return specParts.join(' / ');
            for (const key of ['std1', 'std2', 'std3']) {
                const v = item?.[key];
                const n = parseFloat(v);
                if (!isNaN(n) && n > 0) return String(v).trim();
            }
            const car = parseFloat(item?.car);
            if (!isNaN(car) && car > 0) return String(item.car);
            return '1';
        })(),
        'sterilizationTemp': formatSterilizationTemp(item?.steritemprange),
        'mealCount': (() => {
            const ins = sourceData.quantity_for_instruction_total ?? sourceData.quantity_for_instruction;
            const pln = sourceData.quantity_for_inventory_total ?? sourceData.quantity_for_inventory ?? sourceData.planned_quantity;
            if (ins != null && pln != null && Number(ins) !== Number(pln))
                return `${ins}（指示） / ${pln}（受注）`;
            return ins ?? sourceData.adjusted_quantity ?? pln ?? sourceData.planned_quantity ?? '';
        })(),
        'stockQuantity': sourceData.current_stock?.toFixed(2) || '',
        'orderNumber': sourceData.jobordno || '',
        'coolingEquipment': '□ﾁﾗｰ水槽 □ﾌﾞﾗｽﾄﾁﾗｰ',
        'coolingTemp': '℃',
        'supervisor': '',
        'unit': item?.uni0 || '',
        'previousProcess': firstRout?.prccd || '',
        'storageLocation': firstRout?.whcd || firstRout?.loccd || '',
        'childItem': firstMbom?.citemcd || '',
        'multiplier': firstMbom?.amu || '',
        'childMultiplier': firstMbom?.otp || '',
        'mealType': data.meal_type || 'B',
    };
    
    for (const [fieldId, value] of Object.entries(headerFields)) {
        const element = container.querySelector(`#${fieldId}`);
        if (element) {
            element.textContent = value;
        }
    }
}

/**
 * メインテーブルのヘッダー行を動的に設定（commonInfo対応版）
 * @param {HTMLElement} container - コンテナ要素
 * @param {Object} data - データオブジェクト
 */
function injectMainTableHeaders(container, data) {
    // commonInfo または最初のitemから mboms を取得
    const sourceData = data.commonInfo || (data.items && data.items.length > 0 ? data.items[0] : null);
    const mboms = sourceData?.mboms || [];
    
    // 品目ヘッダー行
    const itemHeaderRow = container.querySelector('#itemHeaderRow');
    if (itemHeaderRow) {
        const itemHeaders = itemHeaderRow.querySelectorAll('th.item-header');
        
        mboms.forEach((mbom, i) => {
            if (i < 10 && itemHeaders[i]) {
                itemHeaders[i].textContent = mbom.child_item?.itemnm || '';
            }
        });
    }
    
    // 単位ヘッダー行
    const unitHeaderRow = container.querySelector('#unitHeaderRow');
    if (unitHeaderRow) {
        const unitHeaders = unitHeaderRow.querySelectorAll('th.unit-header');
        
        mboms.forEach((mbom, i) => {
            if (i < 10 && unitHeaders[i]) {
                unitHeaders[i].textContent = mbom.child_item?.uni?.uninm || '';
            }
        });
    }
}

/**
 * テーブルデータを注入（新テンプレート用）
 * 常に20行を表示する
 * @param {HTMLElement} container - コンテナ要素
 * @param {Object} data - データオブジェクト
 */
function injectTableData(container, data) {
    const tbody = container.querySelector('#facilityTableBody');
    if (!tbody) return;
    
    const items = data.items || [];
    
    // tbodyをクリア
    tbody.innerHTML = '';
    
    // 合計値を先に計算
    let totalStandardBags = 0;
    const totalColumns = new Array(10).fill(0);
    items.forEach(item => {
        totalStandardBags += item.standard_bags || 0;
        const seasoningAmounts = item.seasoning_amounts || [];
        seasoningAmounts.forEach((s, j) => {
            if (j < 10 && s.calculated_amount !== undefined && s.calculated_amount !== null) {
                totalColumns[j] += s.calculated_amount;
            }
        });
    });
    
    // 21行を作成（20行のデータ + 1行の合計）
    for (let i = 0; i < 21; i++) {
        const row = document.createElement('tr');
        const item = items[i];
        
        // 最後の行は合計行
        if (i === 20) {
            row.className = 'total-row';
        }
        
        // 施設名（合計行の場合は「合計」と表示）
        const facilityCell = document.createElement('td');
        const facilityContent = document.createElement('div');
        facilityContent.className = 'cell-content';
        facilityContent.style.justifyContent = 'flex-start'; // 明示的に左寄せ
        if (i === 20) {
            facilityContent.textContent = '合計';
        } else {
            facilityContent.textContent = item ? (item.facility_name || item.shpctrnm || '') : '';
            if (!item) facilityContent.innerHTML = '&nbsp;';
        }
        facilityCell.appendChild(facilityContent);
        row.appendChild(facilityCell);
        
        // 規格袋数（合計行の場合は合計値）
        const standardBagsCell = document.createElement('td');
        const standardBagsContent = document.createElement('div');
        standardBagsContent.className = 'cell-content';
        if (i === 20) {
            standardBagsContent.textContent = totalStandardBags;
        } else {
            standardBagsContent.textContent = item ? (item.standard_bags || '0') : '';
            if (!item) standardBagsContent.innerHTML = '&nbsp;';
        }
        standardBagsCell.appendChild(standardBagsContent);
        row.appendChild(standardBagsCell);
        
        let rowTotal = 0;
        
        // 規格外袋（端数）の各品目 - 10列
        const seasoningAmounts = item?.seasoning_amounts || [];
        
        if (i === 20) {
            // 合計行：各列の合計を表示
            for (let j = 0; j < 10; j++) {
                const amountCell = document.createElement('td');
                const amountContent = document.createElement('div');
                amountContent.className = 'cell-content';
                if (totalColumns[j] > 0) {
                    amountContent.textContent = formatNumber(totalColumns[j]);
                    rowTotal += totalColumns[j];
                } else {
                    amountContent.innerHTML = '&nbsp;';
                }
                amountCell.appendChild(amountContent);
                row.appendChild(amountCell);
            }
        } else {
            // データ行：通常通り
            for (let j = 0; j < 10; j++) {
                const amountCell = document.createElement('td');
                const amountContent = document.createElement('div');
                amountContent.className = 'cell-content';
                const amount = seasoningAmounts[j]?.calculated_amount;
                if (amount !== undefined && amount !== null) {
                    amountContent.textContent = formatNumber(amount);
                    rowTotal += (amount || 0);
                } else {
                    amountContent.innerHTML = '&nbsp;';
                }
                amountCell.appendChild(amountContent);
                row.appendChild(amountCell);
            }
        }
        
        // 計
        const totalCell = document.createElement('td');
        const totalContent = document.createElement('div');
        totalContent.className = 'cell-content';
        if (i === 20) {
            totalContent.textContent = formatNumber(rowTotal);
        } else if (item) {
            totalContent.textContent = formatNumber(rowTotal);
        } else {
            totalContent.innerHTML = '&nbsp;';
        }
        totalCell.appendChild(totalContent);
        row.appendChild(totalCell);
        
        tbody.appendChild(row);
    }
}

/**
 * 工程・レシピ情報テーブル（右上テーブル）にデータを注入
 * 品目全体の合計を表示
 * @param {HTMLElement} container - コンテナ要素
 * @param {Object} data - データオブジェクト
 */
function injectIngredientsTableData(container, data) {
    const tbody = container.querySelector('#ingredientsTableBody');
    if (!tbody) return;
    
    // commonInfo または最初のitem
    const sourceData = data.commonInfo || (data.items && data.items.length > 0 ? data.items[0] : null);
    const mboms = sourceData?.mboms || [];
    const displayRows = data.ingredient_display_rows;

    // 投入量登録反映：API の ingredient_display_rows で右上を上書き
    if (displayRows && displayRows.length > 0) {
        const rows = tbody.querySelectorAll('tr');
        const dataRows = Array.from(rows).slice(0, -1);
        dataRows.forEach((row, i) => {
            const cells = row.querySelectorAll('td');
            if (cells.length < 6) return;
            const dr = displayRows[i];
            const mbom = mboms[i];
            const childRout = mbom?.child_item?.routs?.[0];
            cells[0].textContent = childRout?.workc?.wcnm || '';
            cells[1].textContent = childRout?.ware?.whnm || '';
            cells[2].textContent = mbom?.child_item?.itemnm || dr?.citemcd || '';
            if (dr) {
                cells[3].textContent = dr.spec_qty != null && dr.spec_qty !== '' ? formatNumber(dr.spec_qty) : '';
                cells[4].textContent = dr.total_qty != null ? formatNumber(dr.total_qty) : '';
                cells[5].textContent = dr.unit_name || mbom?.child_item?.uni?.uninm || '';
            }
        });
        const totalRow = tbody.querySelector('tr:last-child');
        if (totalRow) {
            const totalStandardBagsCell = totalRow.querySelector('#totalStandardBagsTop');
            const totalIrregularBagsCell = totalRow.querySelector('#totalIrregularBagsTop');
            if (totalStandardBagsCell)
                totalStandardBagsCell.textContent = data.totals?.standard_bags || '0';
            if (totalIrregularBagsCell)
                totalIrregularBagsCell.textContent = data.totals?.irregular_bags || '0';
        }
        return;
    }
    
    // 品目全体の合計数量を使用（totalsから取得）
    const totalOrderQuantity = data.totals?.grand_total || 0;
    
    // 既存の行を取得（最後の行は合計行なので除外）
    const rows = tbody.querySelectorAll('tr');
    const dataRows = Array.from(rows).slice(0, -1);
    
    // 各行にデータを注入
    dataRows.forEach((row, i) => {
        const cells = row.querySelectorAll('td');
        if (cells.length < 6) return;
        
        const mbom = mboms[i];
        
        if (mbom) {
            const childRout = mbom?.child_item?.routs?.[0];
            
            cells[0].textContent = childRout?.workc?.wcnm || '';
            cells[1].textContent = childRout?.ware?.whnm || '';
            cells[2].textContent = mbom.child_item?.itemnm || '';
            cells[3].textContent = '';  // 規格数量は空欄
            
            // 総数量 = (品目全体の合計数量 / otp) × amu
            let totalQuantityForItem = '';
            if (mbom?.otp && mbom.otp > 0) {
                const amu = mbom?.amu || 0;
                const calculatedTotal = (totalOrderQuantity / mbom.otp) * amu;
                totalQuantityForItem = formatNumber(calculatedTotal);
            }
            cells[4].textContent = totalQuantityForItem;
            cells[5].textContent = mbom?.child_item?.uni?.uninm || '';
        }
    });
    
    // 最終行の合計を更新（品目全体の合計）
    const totalRow = tbody.querySelector('tr:last-child');
    if (totalRow) {
        const totalStandardBagsCell = totalRow.querySelector('#totalStandardBagsTop');
        const totalIrregularBagsCell = totalRow.querySelector('#totalIrregularBagsTop');
        
        if (totalStandardBagsCell) {
            // 品目全体の規格袋合計
            totalStandardBagsCell.textContent = data.totals?.standard_bags || '0';
        }
        if (totalIrregularBagsCell) {
            // 品目全体の規格外袋合計
            totalIrregularBagsCell.textContent = data.totals?.irregular_bags || '0';
        }
    }
}

/**
 * 数値をフォーマットする（小数点2桁）
 * @param {number} value - フォーマット対象の数値
 * @returns {string} フォーマットされた文字列
 */
function formatNumber(value) {
    if (value === null || value === undefined || value === '') return '';
    const num = typeof value === 'number' ? value : parseFloat(value);
    if (isNaN(num)) return '';
    return num.toFixed(2);
}

/**
 * 品目全体の合計値を計算
 * @param {Array} items - 品目内の全アイテム
 * @returns {Object} 合計値
 */
function calculateItemTotals(items) {
    let totalStandardBags = 0;
    let totalIrregularBags = 0;
    let grandTotal = 0;
    const seasoningColumns = new Array(10).fill(0);
    
    items.forEach(item => {
        totalStandardBags += item.standard_bags || 0;
        if ((item.irregular_quantity || 0) > 0) {
            totalIrregularBags += 1;
        }
        grandTotal += (item.quantity_for_instruction ?? item.adjusted_quantity ?? item.planned_quantity) || 0;
        
        // 調味液の列ごとの合計を計算
        const seasoningAmounts = item.seasoning_amounts || [];
        seasoningAmounts.forEach((s, idx) => {
            if (idx < 10 && s.calculated_amount !== undefined) {
                seasoningColumns[idx] += s.calculated_amount;
            }
        });
    });
    
    return {
        standard_bags: totalStandardBags,
        irregular_bags: totalIrregularBags,
        grand_total: grandTotal,
        seasoning_columns: seasoningColumns
    };
}

/**
 * 袋詰指示書用のデータを準備（ページング対応版）
 * APIレスポンスからテンプレート用のデータ構造に変換
 * 品目ごとにグループ化し、20件ごとにページング
 * @param {Object} apiResponse - APIレスポンスデータ
 * @returns {Array} ページデータ配列
 */
export function prepareBaggingInstructionData(apiResponse) {
    const items = apiResponse.items || [];
    const ingredientDisplayRows = apiResponse.ingredient_display_rows || null;
    
    if (items.length === 0) {
        return [{
            items: [],
            ingredient_display_rows: ingredientDisplayRows,
            totals: {
                standard_bags: 0,
                irregular_bags: 0,
                grand_total: 0,
                seasoning_columns: new Array(10).fill(0)
            },
            pageNumber: 1,
            totalPages: 1
        }];
    }
    
    // ステップ1: 品目コードでグループ化
    const groupedByItem = {};
    items.forEach(item => {
        const itemcd = item.itemcd;
        if (!groupedByItem[itemcd]) {
            groupedByItem[itemcd] = {
                items: [],
                commonInfo: null
            };
        }
        groupedByItem[itemcd].items.push(item);
        
        // 最初のitemから品目共通情報を保持
        if (!groupedByItem[itemcd].commonInfo) {
            groupedByItem[itemcd].commonInfo = {
                itemcd: item.itemcd,
                itemnm: item.itemnm,
                prddt: item.prddt,
                delvedt: item.delvedt,
                shptm: item.shptm,
                jobordno: item.jobordno,
                jobordmernm: item.jobordmernm,
                current_stock: item.current_stock || 0,
                planned_quantity: item.planned_quantity,
                adjusted_quantity: item.adjusted_quantity,
                quantity_for_inventory: item.quantity_for_inventory,
                quantity_for_instruction: item.quantity_for_instruction,
                // リレーション情報
                item: item.item,
                shpctr: item.shpctr,
                mboms: item.mboms,
                cusmcd: item.cusmcd,
            };
        }
    });
    
    const allPages = [];
    
    // ステップ2: 各品目ごとにページング処理
    Object.keys(groupedByItem).forEach(itemcd => {
        const group = groupedByItem[itemcd];
        const itemList = group.items;
        const commonInfo = group.commonInfo;
        
        const qtyInvTotal = itemList.reduce((s, x) => s + (Number(x.quantity_for_inventory) || Number(x.planned_quantity) || 0), 0);
        const qtyInsTotal = itemList.reduce((s, x) => s + (Number(x.quantity_for_instruction) || Number(x.adjusted_quantity) || 0), 0);
        commonInfo.quantity_for_inventory_total = qtyInvTotal;
        commonInfo.quantity_for_instruction_total = qtyInsTotal;

        // 品目全体の合計値を計算（全ページで使用）
        const itemTotals = calculateItemTotals(itemList);
        
        // ステップ3: 20件ごとにページング
        const pageSize = 20;
        const totalPages = Math.ceil(itemList.length / pageSize);
        
        for (let pageIndex = 0; pageIndex < totalPages; pageIndex++) {
            const startIdx = pageIndex * pageSize;
            const endIdx = Math.min(startIdx + pageSize, itemList.length);
            const pageItems = itemList.slice(startIdx, endIdx);
            
            // 表示用にitemデータを整形
            const processedItems = pageItems.map(item => ({
                facility_name: item.shpctrnm || '',
                standard_bags: item.standard_bags || 0,
                irregular_quantity: item.irregular_quantity || 0,
                total: item.adjusted_quantity || item.planned_quantity || 0,
                shpctrnm: item.shpctrnm || '',
                seasoning_amounts: item.seasoning_amounts || [],
                item: item.item,
                shpctr: item.shpctr,
                routs: item.routs,
                mboms: item.mboms,
                cusmcd: item.cusmcd,
                prddt: item.prddt,
                delvedt: item.delvedt,
                shptm: item.shptm,
                itemcd: item.itemcd,
                itemnm: item.itemnm,
                jobordno: item.jobordno,
                jobordmernm: item.jobordmernm,
                planned_quantity: item.planned_quantity,
                adjusted_quantity: item.adjusted_quantity,
                current_stock: item.current_stock || 0,
            }));
            
            allPages.push({
                // ページ情報
                pageNumber: pageIndex + 1,
                totalPages: totalPages,
                itemcd: itemcd,
                
                // 品目共通情報（全ページで同じ）
                commonInfo: commonInfo,
                
                // このページのデータ
                items: processedItems,
                
                ingredient_display_rows: ingredientDisplayRows,

                // 合計値（品目全体の合計）
                totals: {
                    standard_bags: itemTotals.standard_bags,
                    irregular_bags: itemTotals.irregular_bags,
                    grand_total: itemTotals.grand_total,
                    facility_count: itemList.length,
                    seasoning_columns: itemTotals.seasoning_columns
                }
            });
        }
    });
    
    return allPages;
}

/**
 * ラベル用のデータを準備
 * APIレスポンスからラベル用のデータ構造に変換
 * @param {Object} apiResponse - APIレスポンスデータ
 * @returns {Object} ラベル用データ
 */
/**
 * API POST /api/bagging/calculate print_type=label → LabelResponseDto（label_type, count, …）。
 * 従来の袋詰指示書形 items（standard_bags）も互換で受ける。
 */
export function prepareLabelData(apiResponse) {
    const items = apiResponse.items || [];
    const labels = [];

    if (items.length === 0) {
        return { labels };
    }

    const first = items[0];
    const isLabelResponse =
        first.label_type !== undefined ||
        first.labelType !== undefined;

    if (isLabelResponse) {
        items.forEach(it => {
            const lt = it.label_type || it.labelType || 'standard';
            const count = Math.max(0, Number(it.count) || 0);
            const fill =
                it.standard_fill_qty != null && it.standard_fill_qty !== ''
                    ? Number(it.standard_fill_qty)
                    : it.kikunip != null
                      ? Number(it.kikunip)
                      : null;

            if (lt === 'standard') {
                for (let i = 0; i < count; i++) {
                    labels.push({
                        type: 'standard',
                        product_name: it.itemnm || '',
                        product_code: it.itemcd || '',
                        eating_date: it.delvedt || '',
                        eating_time: it.shptm || '',
                        expiry_date: it.expiry_date || '',
                        strtemp: it.strtemp || '',
                        standard_fill_qty: fill,
                        facility_name: it.shpctrnm || ''
                    });
                }
            } else if (lt === 'irregular') {
                const iq = it.irregular_quantity != null ? Number(it.irregular_quantity) : 0;
                if (iq > 0) {
                    labels.push({
                        type: 'irregular',
                        product_name: it.itemnm || '',
                        product_code: it.itemcd || '',
                        eating_date: it.delvedt || '',
                        eating_time: it.shptm || '',
                        expiry_date: it.expiry_date || '',
                        strtemp: it.strtemp || '',
                        irregular_quantity: iq,
                        facility_name: it.shpctrnm || ''
                    });
                }
            }
        });
        return { labels };
    }

    // Legacy: 指示書計算結果と同形の items
    items.forEach(item => {
        const standardBags = item.standard_bags || 0;
        const irregularQuantity = item.irregular_quantity || 0;

        for (let i = 0; i < standardBags; i++) {
            labels.push({
                type: 'standard',
                product_name: item.itemnm || item.product_name || '',
                product_code: item.itemcd || item.product_code || '',
                eating_date: item.delvedt || item.eating_date || '',
                eating_time: item.shptm || item.eating_time || '',
                expiry_date: item.expiry_date || '',
                strtemp: '',
                standard_fill_qty: null,
                standard_quantity: item.adjusted_quantity || item.planned_quantity || 0,
                facility_name: item.shpctrnm || item.facility_name || ''
            });
        }

        if (irregularQuantity > 0) {
            labels.push({
                type: 'irregular',
                product_name: item.itemnm || item.product_name || '',
                product_code: item.itemcd || item.product_code || '',
                eating_date: item.delvedt || item.eating_date || '',
                eating_time: item.shptm || item.eating_time || '',
                expiry_date: item.expiry_date || '',
                strtemp: '',
                irregular_quantity: irregularQuantity,
                facility_name: item.shpctrnm || item.facility_name || ''
            });
        }
    });

    return { labels };
}

function escapeLabelHtml(s) {
    if (s == null || s === '') return '';
    return String(s)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

/**
 * ラベルHTMLにデータを注入
 * @param {string} templateHtml - HTMLテンプレート文字列
 * @param {Object} data - 注入するデータ
 * @returns {HTMLElement} データが注入されたHTML要素
 */
export function injectLabelData(templateHtml, data) {
    // DOMParserを使って正しくHTMLをパース
    const parser = new DOMParser();
    const doc = parser.parseFromString(templateHtml, 'text/html');
    
    const container = doc.querySelector('#labelsContainer');
    if (!container) {
        // コンテナがない場合は空のラッパーを返す
        const wrapper = document.createElement('div');
        wrapper.className = 'print-wrapper';
        return wrapper;
    }
    
    // コンテナをクリア
    container.innerHTML = '';
    
    // ラベルを生成
    const labels = data.labels || [];
    labels.forEach(label => {
        const labelDiv = document.createElement('div');
        labelDiv.className = `label label-type-${label.type}`;
        
        if (label.type === 'standard') {
            const specQty =
                label.standard_fill_qty != null && !Number.isNaN(label.standard_fill_qty)
                    ? formatNumber(label.standard_fill_qty)
                    : label.standard_quantity != null
                      ? formatNumber(label.standard_quantity)
                      : '-';
            const st = escapeLabelHtml(label.strtemp);
            labelDiv.innerHTML = `
                <div class="label-header">規格品</div>
                <div class="label-row">
                    <span class="label-title">品目:</span>
                    <span class="label-value">${escapeLabelHtml(label.product_name)} (${escapeLabelHtml(label.product_code)})</span>
                </div>
                <div class="label-row">
                    <span class="label-title">喫食日:</span>
                    <span class="label-value">${escapeLabelHtml(label.eating_date)} ${escapeLabelHtml(label.eating_time)}</span>
                </div>
                <div class="label-row">
                    <span class="label-title">賞味期限:</span>
                    <span class="label-value">${escapeLabelHtml(label.expiry_date) || '-'}</span>
                </div>
                <div class="label-row">
                    <span class="label-title">殺菌温度:</span>
                    <span class="label-value">${st || '-'}</span>
                </div>
                <div class="label-row">
                    <span class="label-title">規格量:</span>
                    <span class="label-value">${specQty}</span>
                </div>
            `;
        } else {
            const st = escapeLabelHtml(label.strtemp);
            labelDiv.innerHTML = `
                <div class="label-header">端数</div>
                <div class="label-row">
                    <span class="label-title">品目:</span>
                    <span class="label-value">${escapeLabelHtml(label.product_name)} (${escapeLabelHtml(label.product_code)})</span>
                </div>
                <div class="label-row">
                    <span class="label-title">喫食日:</span>
                    <span class="label-value">${escapeLabelHtml(label.eating_date)} ${escapeLabelHtml(label.eating_time)}</span>
                </div>
                <div class="label-row">
                    <span class="label-title">賞味期限:</span>
                    <span class="label-value">${escapeLabelHtml(label.expiry_date) || '-'}</span>
                </div>
                <div class="label-row">
                    <span class="label-title">殺菌温度:</span>
                    <span class="label-value">${st || '-'}</span>
                </div>
                <div class="label-row">
                    <span class="label-title">施設:</span>
                    <span class="label-value">${escapeLabelHtml(label.facility_name)}</span>
                </div>
                <div class="label-row">
                    <span class="label-title">端数:</span>
                    <span class="label-value">${formatNumber(label.irregular_quantity)}</span>
                </div>
            `;
        }
        
        container.appendChild(labelDiv);
    });
    
    // スタイルタグを抽出
    const styles = doc.querySelectorAll('style');
    
    // body内のコンテンツを取得
    const bodyContent = doc.body.cloneNode(true);
    
    // ラッパーコンテナを作成してスタイルとコンテンツを追加
    const wrapper = document.createElement('div');
    wrapper.className = 'print-wrapper';
    
    // スタイルを追加（スコープ化）
    styles.forEach(style => {
        const scopedStyle = document.createElement('style');
        // すべてのセレクタに #print-preview-container プレフィックスを追加してスコープ化
        scopedStyle.textContent = scopeStylesheet(style.textContent);
        wrapper.appendChild(scopedStyle);
    });
    
    // bodyの内容を追加
    while (bodyContent.firstChild) {
        wrapper.appendChild(bodyContent.firstChild);
    }
    
    return wrapper;
}
