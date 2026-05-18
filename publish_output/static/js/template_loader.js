/**
 * テンプレート読み込み・データ注入モジュール
 */

/**
 * 袋詰指示書ヘッダー用の喫食時間の値（API の eating_time_label）。JSON は snake_case / camelCase の両方を読む。
 * @param {Record<string, unknown>|null|undefined} source
 * @returns {string}
 */
function resolveEatingTimeLabel(source) {
    if (!source || typeof source !== 'object') return '';
    return String(
        source.eating_time_label ?? source.eatingTimeLabel ?? ''
    ).trim();
}

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
    
    // メインテーブルのヘッダー行を動的に設定
    injectMainTableHeaders(doc, data);

    // テーブルデータを注入（合計行も含む）→ オーバーフロー補正後の合計を取得
    const correctedTotals = injectTableData(doc, data);

    // 工程・レシピ情報テーブル（右上）を注入（補正済み規格袋合計を渡す）
    injectIngredientsTableData(doc, data, correctedTotals);
    
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
    
    // 喫食日・喫食時間の値のみ（ラベル「喫食時間:」は付けない。API eating_time_label）
    const eatingDateElement = container.querySelector('#eatingDateInfo');
    if (eatingDateElement && sourceData) {
        const delvedt = sourceData.delvedt || '';
        const eatingTimeLabel = resolveEatingTimeLabel(sourceData);
        let datePart = `喫食日:${delvedt}`;
        if (eatingTimeLabel) {
            datePart += `　${eatingTimeLabel}`;
        }
        eatingDateElement.textContent = datePart;
    }
    
    const headerFields = {
        'productionDate': sourceData.prddt || '',
        'productDate': sourceData.delvedt || '',
        'kitCode': sourceData.itemcd || '',
        'kitName': sourceData.jobordmernm || '',
        'cookingType': '',
        'grade': (() => {
            const savedSpec = data?.ingredient_display_rows?.[0]?.spec_qty;
            if (savedSpec != null && savedSpec !== '') return String(savedSpec);
            const specParts = [
                item?.classification1_code ?? item?.spec1,
                item?.classification2_code ?? item?.spec2,
                item?.classification3_code ?? item?.spec3,
            ].filter(Boolean);
            if (specParts.length) return specParts.join(' / ');
            if (item?.std && String(item.std).trim()) return String(item.std).trim();
            for (const key of ['car1', 'car2', 'car3']) {
                const v = item?.[key];
                const n = parseFloat(v);
                if (!isNaN(n) && n > 0) return String(v).trim();
            }
            const kikunip = parseFloat(item?.kikunip);
            if (!isNaN(kikunip) && kikunip > 0) return String(item.kikunip);
            const car = parseFloat(item?.car);
            if (!isNaN(car) && car > 0) return String(item.car);
            return '1';
        })(),
        'sterilizationTemp': formatSterilizationTemp(item?.steritemprange),
        'mealCount': '',
        'stockQuantity': sourceData.current_stock?.toFixed(2) || '',
        'orderNumber': '',
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
 * 規格数量（1袋あたりの充填量）を解決する。
 * ingredient_display_rows[0].spec_qty を優先し、なければ Car → kikunip へフォールバック。
 */
function resolveSpecQty(data) {
    const savedSpec = data?.ingredient_display_rows?.[0]?.spec_qty;
    if (savedSpec != null && savedSpec !== '') {
        const n = parseFloat(savedSpec);
        if (!isNaN(n) && n > 0) return n;
    }
    const item = data?.commonInfo?.item ?? data?.items?.[0]?.item;
    for (const key of ['car1', 'car2', 'car3']) {
        const v = parseFloat(item?.[key]);
        if (!isNaN(v) && v > 0) return v;
    }
    const kikunip = parseFloat(item?.kikunip);
    if (!isNaN(kikunip) && kikunip > 0) return kikunip;
    return 0;
}

/** 整数は整数表示、小数は小数点2桁表示。 */
function formatBagCount(n) {
    if (n == null || n === '' || isNaN(Number(n))) return '';
    const num = Number(n);
    if (num === 0) return '';
    return Number.isInteger(num) ? String(num) : formatNumber(num);
}

function resolveMainTableSizing(itemCount) {
    const defaultRowHeight = 22;
    const defaultBodyRowsThatFit = 26; // 25 facilities + total row fits on A4.
    const bodyRows = Math.max(21, itemCount + 1);
    const rowHeight = Math.max(
        12,
        Math.min(defaultRowHeight, Math.floor((defaultBodyRowsThatFit * defaultRowHeight) / bodyRows))
    );

    return {
        rowHeight,
        cellFontSize: Math.max(7, Math.min(11, rowHeight - 7)),
        irregularFontSize: Math.max(6, Math.min(9, rowHeight - 9)),
        lineHeight: Math.max(8, rowHeight - 8),
    };
}

/**
 * テーブルデータを注入（新テンプレート用）
 * 空行で水増しせず、データ行の直後に合計行を表示する
 * @param {HTMLElement} container - コンテナ要素
 * @param {Object} data - データオブジェクト
 */
function injectTableData(container, data) {
    const tbody = container.querySelector('#facilityTableBody');
    if (!tbody) return;

    const items = data.items || [];
    tbody.innerHTML = '';

    const specQty = resolveSpecQty(data);
    const mainTable = container.querySelector('.main-table');
    if (mainTable) {
        const sizing = resolveMainTableSizing(items.length);
        mainTable.style.setProperty('--bagging-main-row-height', `${sizing.rowHeight}px`);
        mainTable.style.setProperty('--bagging-main-cell-font-size', `${sizing.cellFontSize}px`);
        mainTable.style.setProperty('--bagging-irregular-font-size', `${sizing.irregularFontSize}px`);
        mainTable.style.setProperty('--bagging-main-line-height', `${sizing.lineHeight}px`);
    }

    // 一番左の子品目の単位が g または kg かどうかを判定
    const mboms = data.commonInfo?.mboms ?? data.items?.[0]?.mboms ?? [];
    const firstMbom = mboms[0];
    const firstUnitNm = (firstMbom?.child_item?.uni?.uninm || '').toLowerCase().trim();
    const useGKgMode = firstUnitNm === 'g' || firstUnitNm === 'kg';
    // 一番左の品目が個数系（切/尾/枚/ヶ等）の場合：j=0列は端数個数、j≥1列はBOM比率で算出
    const useCountMode = !useGKgMode && firstMbom != null;

    // 施設ごとの規格袋数・端数を計算
    // バックエンドが按分等を適用して計算した standard_bags/irregular_quantity を優先使用
    const computed = items.map(item => {
        const rawQty = Number(item.planned_quantity) || Number(item.adjusted_quantity) || 0;
        let stdBags = item.standard_bags != null ? (item.standard_bags || 0)
                    : specQty > 0 ? Math.floor(rawQty / specQty)
                    : 0;
        let irregular = item.irregular_quantity != null ? Number(item.irregular_quantity) || 0
                      : specQty > 0 ? rawQty % specQty
                      : 0;
        // 浮動小数点誤差で irregular >= specQty になる場合の安全策
        if (specQty > 0 && irregular >= specQty) {
            stdBags += Math.floor(irregular / specQty);
            irregular = irregular % specQty;
        }
        // g/kg モード：有効配分量（stdBags × specQty + 端数）に対する端数の比率
        const effectiveQty = specQty > 0 ? (stdBags * specQty + irregular) : rawQty;
        const ratio = (useGKgMode && effectiveQty > 0) ? irregular / effectiveQty : null;
        return { stdBags, irregular, ratio, qty: effectiveQty };
    });

    // g/kgモード：各品目端数分を先行計算し、合計がspecQtyを超えた場合は規格袋を補正する
    const extendedComputed = computed.map((c, idx) => {
        const seasoningAmounts = items[idx]?.seasoning_amounts || [];
        let displayStdBags = c.stdBags;
        let displayIrregular = c.irregular;
        let childAmounts = null;

        if (useGKgMode && c.ratio != null) {
            const amounts = [];
            for (let j = 0; j < 10; j++) {
                const rawAmount = seasoningAmounts[j]?.calculated_amount;
                amounts.push(rawAmount != null ? rawAmount * c.ratio : null);
            }
            const childTotal = amounts.reduce((sum, v) => sum + (v || 0), 0);
            if (specQty > 0 && childTotal > specQty) {
                // 各品目端数分合計が規格を超えた → 規格袋を追加し余りを新たな端数とする
                const extraBags = Math.floor(childTotal / specQty);
                displayStdBags += extraBags;
                const newTotal = childTotal % specQty;
                const scale = childTotal > 0 ? newTotal / childTotal : 0;
                childAmounts = amounts.map(v => v != null ? v * scale : null);
                displayIrregular = newTotal;
            } else {
                childAmounts = amounts;
            }
        }

        return { ...c, displayIrregular, displayStdBags, childAmounts };
    });

    // 合計値
    let totalStandardBags = 0;
    let totalIrregular = 0;
    const totalColumns = new Array(10).fill(0);
    extendedComputed.forEach((c, idx) => {
        totalStandardBags += c.displayStdBags;
        totalIrregular += c.displayIrregular;
        const seasoningAmounts = items[idx]?.seasoning_amounts || [];
        if (useGKgMode) {
            if (c.childAmounts) {
                c.childAmounts.forEach((amt, j) => {
                    if (j < 10 && amt != null) totalColumns[j] += amt;
                });
            }
        } else if (useCountMode) {
            // j=0：端数個数そのもの、j≥1：(端数 / otp) * amu
            totalColumns[0] += c.irregular;
            for (let j = 1; j < mboms.length && j < 10; j++) {
                const otp = Number(mboms[j]?.otp) || 0;
                const amu = Number(mboms[j]?.amu) || 0;
                if (otp > 0) totalColumns[j] += (c.irregular / otp) * amu;
            }
        } else {
            seasoningAmounts.forEach((s, j) => {
                if (j < 10 && s.calculated_amount != null)
                    totalColumns[j] += s.calculated_amount;
            });
        }
    });

    // 空行で水増しすると合計行だけが2ページ目に送られるため、データ行の直後に合計行を置く。
    const totalRowIndex = items.length;
    for (let i = 0; i <= totalRowIndex; i++) {
        const row = document.createElement('tr');
        const item = items[i];
        const c = extendedComputed[i];

        if (i === totalRowIndex) row.className = 'total-row';

        // 施設名
        const facilityCell = document.createElement('td');
        const facilityContent = document.createElement('div');
        facilityContent.className = 'cell-content';
        facilityContent.style.justifyContent = 'flex-start';
        if (i === totalRowIndex) {
            facilityContent.textContent = '合計';
        } else {
            const facilityText = item ? (item.facility_name || item.shpctrnm || '') : '';
            facilityContent.textContent = facilityText || ' ';
            // 施設名列幅: 9% of 180mm ≈ 61px、セル内パディング 6px 分を引いた 55px に収まるよう縮小
            if (facilityText) {
                const baseFontSize = parseFloat(mainTable?.style.getPropertyValue('--bagging-main-cell-font-size')) || 11;
                const shrunkSize = shrinkFontSizeToFit(facilityText, 55, baseFontSize, 'MS Gothic', 6);
                if (shrunkSize < baseFontSize) facilityContent.style.fontSize = `${shrunkSize}px`;
            }
        }
        facilityCell.appendChild(facilityContent);
        row.appendChild(facilityCell);

        // 規格袋数（受注数 ÷ 規格数量 の商）
        const standardBagsCell = document.createElement('td');
        const standardBagsContent = document.createElement('div');
        standardBagsContent.className = 'cell-content';
        if (i === totalRowIndex) {
            standardBagsContent.textContent = totalStandardBags > 0 ? String(totalStandardBags) : '';
        } else if (item) {
            standardBagsContent.textContent = c.displayStdBags > 0 ? String(c.displayStdBags) : '';
        } else {
            standardBagsContent.innerHTML = '&nbsp;';
        }
        standardBagsCell.appendChild(standardBagsContent);
        row.appendChild(standardBagsCell);

        // 規格外袋（端数）内の各調味液列 - 10列
        const seasoningAmounts = item?.seasoning_amounts || [];
        if (i === totalRowIndex) {
            for (let j = 0; j < 10; j++) {
                const amountCell = document.createElement('td');
                const amountContent = document.createElement('div');
                amountContent.className = 'cell-content';
                amountContent.innerHTML = totalColumns[j] > 0 ? formatNumber(totalColumns[j]) : '&nbsp;';
                amountCell.appendChild(amountContent);
                row.appendChild(amountCell);
            }
        } else {
            for (let j = 0; j < 10; j++) {
                const amountCell = document.createElement('td');
                const amountContent = document.createElement('div');
                amountContent.className = 'cell-content';
                let displayAmount;
                if (useGKgMode) {
                    if (c.childAmounts && c.childAmounts[j] != null)
                        displayAmount = c.childAmounts[j];
                } else if (useCountMode) {
                    if (j === 0) {
                        // 一番左の個数系品目列：端数個数
                        displayAmount = c.irregular > 0 ? c.irregular : null;
                    } else if (j < mboms.length) {
                        // 右側の調味料列：(端数 / otp) * amu
                        const otp = Number(mboms[j]?.otp) || 0;
                        const amu = Number(mboms[j]?.amu) || 0;
                        displayAmount = (otp > 0 && c.irregular > 0) ? (c.irregular / otp) * amu : null;
                    }
                } else {
                    displayAmount = seasoningAmounts[j]?.calculated_amount;
                }
                amountContent.innerHTML = (displayAmount != null && displayAmount !== 0) ? formatNumber(displayAmount) : '&nbsp;';
                amountCell.appendChild(amountContent);
                row.appendChild(amountCell);
            }
        }

        // 端数列（g/kgモード：調味液列合計の規格数量余り、それ以外：受注数 ÷ 規格数量 の余り）
        const irregularCell = document.createElement('td');
        const irregularContent = document.createElement('div');
        irregularContent.className = 'cell-content';
        if (i === totalRowIndex) {
            irregularContent.innerHTML = totalIrregular > 0 ? formatBagCount(totalIrregular) : '&nbsp;';
        } else if (item) {
            irregularContent.innerHTML = c.displayIrregular > 0 ? formatBagCount(c.displayIrregular) : '&nbsp;';
        } else {
            irregularContent.innerHTML = '&nbsp;';
        }
        irregularCell.appendChild(irregularContent);
        row.appendChild(irregularCell);

        tbody.appendChild(row);
    }

    // オーバーフロー補正後の合計を返す（右上の規格袋合計に使用）
    const totalIrregularBagCount = extendedComputed.filter(c => c.displayIrregular > 0).length;
    return { totalStandardBags, totalIrregularBagCount };
}

/**
 * 工程・レシピ情報テーブル（右上テーブル）にデータを注入
 * 品目全体の合計を表示
 * @param {HTMLElement} container - コンテナ要素
 * @param {Object} data - データオブジェクト
 * @param {{totalStandardBags: number, totalIrregularBagCount: number}|null} correctedTotals - 下段補正済み合計
 */
function injectIngredientsTableData(container, data, correctedTotals = null) {
    const tbody = container.querySelector('#ingredientsTableBody');
    if (!tbody) return;

    // commonInfo または最初のitem
    const sourceData = data.commonInfo || (data.items && data.items.length > 0 ? data.items[0] : null);
    const mboms = sourceData?.mboms || [];
    const displayRows = data.ingredient_display_rows;

    // 右上合計欄の計算：下テーブルと同じロジックで施設ごとに集計
    const allItems = data.items || [];
    const firstMbomTop = data.commonInfo?.mboms?.[0] ?? allItems[0]?.mboms?.[0];
    const firstUnitTop = (firstMbomTop?.child_item?.uni?.uninm || '').toLowerCase().trim();
    const useGKgTop = firstUnitTop === 'g' || firstUnitTop === 'kg';
    const specQtyTop = resolveSpecQty(data);

    let computedTotalStdBags = 0;
    let computedIrregularBagCount = 0;
    allItems.forEach(item => {
        let stdBags = item.standard_bags != null ? (item.standard_bags || 0)
                    : specQtyTop > 0 ? Math.floor((Number(item.planned_quantity) || 0) / specQtyTop)
                    : 0;
        let irregular = item.irregular_quantity != null ? Number(item.irregular_quantity) || 0
                      : specQtyTop > 0 ? (Number(item.planned_quantity) || 0) % specQtyTop
                      : 0;
        if (specQtyTop > 0 && irregular >= specQtyTop) {
            stdBags += Math.floor(irregular / specQtyTop);
            irregular = irregular % specQtyTop;
        }
        computedTotalStdBags += stdBags;
        if (irregular > 0) computedIrregularBagCount += 1;
    });

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
            const finalStdBags = correctedTotals ? correctedTotals.totalStandardBags : computedTotalStdBags;
            const finalIrregularCount = correctedTotals ? correctedTotals.totalIrregularBagCount : computedIrregularBagCount;
            if (totalStandardBagsCell)
                totalStandardBagsCell.textContent = finalStdBags > 0 ? String(finalStdBags) : '';
            if (totalIrregularBagsCell)
                totalIrregularBagsCell.textContent = finalIrregularCount > 0 ? String(finalIrregularCount) : '';
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

    // 最終行の合計を更新
    const totalRow = tbody.querySelector('tr:last-child');
    if (totalRow) {
        const totalStandardBagsCell = totalRow.querySelector('#totalStandardBagsTop');
        const totalIrregularBagsCell = totalRow.querySelector('#totalIrregularBagsTop');
        if (totalStandardBagsCell)
            totalStandardBagsCell.textContent = computedTotalStdBags > 0 ? String(computedTotalStdBags) : '';
        if (totalIrregularBagsCell)
            totalIrregularBagsCell.textContent = computedIrregularBagCount > 0 ? String(computedIrregularBagCount) : '';
    }
}

/**
 * テキストが指定幅（px）に収まるよう、フォントサイズを最小値まで下げて返す。
 * Canvas API で実測し、利用不可の場合は文字幅の近似値（全角=1em, 半角=0.6em）で代替する。
 * @param {string} text
 * @param {number} targetWidthPx - 収めたい幅（px）
 * @param {number} baseFontPx - 基準フォントサイズ（px）
 * @param {string} fontFamily
 * @param {number} minFontPx - 最小フォントサイズ（px）
 * @returns {number} 適用すべきフォントサイズ（px）
 */
function shrinkFontSizeToFit(text, targetWidthPx, baseFontPx, fontFamily, minFontPx) {
    if (!text) return baseFontPx;
    let fontSize = baseFontPx;
    try {
        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d');
        while (fontSize > minFontPx) {
            ctx.font = `${fontSize}px "${fontFamily}", monospace`;
            if (ctx.measureText(text).width <= targetWidthPx) break;
            fontSize -= 0.5;
        }
    } catch (_) {
        // canvas 未対応環境: 全角1em・半角0.6em の近似で代替
        const measure = (fs) => [...text].reduce((w, ch) => w + (ch.charCodeAt(0) > 0xFF ? fs : fs * 0.6), 0);
        while (fontSize > minFontPx && measure(fontSize) > targetWidthPx) fontSize -= 0.5;
    }
    return fontSize;
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

function resolveStandardBags(item) {
    // 子品目が1個の場合はバックエンドの計算結果を優先（TotalQty入力時の按分計算を反映する）
    const mboms = item.mboms ?? [];
    if (mboms.length === 1 && item.standard_bags != null) {
        return item.standard_bags || 0;
    }
    const std = parseFloat(item.item?.std);
    if (!isNaN(std) && std > 0) {
        const qty = item.planned_quantity ?? 0;
        return Math.floor(qty / std);
    }
    return item.standard_bags || 0;
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
        totalStandardBags += resolveStandardBags(item);
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
 * 袋詰指示書用のデータを準備
 * APIレスポンスからテンプレート用のデータ構造に変換
 * 品目ごとにグループ化し、1品目を1印刷ページにまとめる
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
    
    // ステップ1: 品目コード × addinfo05 でグループ化（addinfo05 が異なるものは別ページ）
    const groupedByItem = {};
    items.forEach(item => {
        const itemcd = item.itemcd;
        const addinfo05Key = item.addinfo05 ?? '';
        const delvEdtKey = item.delvedt ?? '';
        const groupKey = `${itemcd}__${addinfo05Key}__${delvEdtKey}`;
        if (!groupedByItem[groupKey]) {
            groupedByItem[groupKey] = {
                items: [],
                commonInfo: null
            };
        }
        groupedByItem[groupKey].items.push(item);

        // 最初のitemから品目共通情報を保持
        if (!groupedByItem[groupKey].commonInfo) {
            groupedByItem[groupKey].commonInfo = {
                itemcd: item.itemcd,
                itemnm: item.itemnm,
                prddt: item.prddt,
                delvedt: item.delvedt,
                shptm: item.shptm,
                eating_time_label: resolveEatingTimeLabel(item),
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
    
    // ステップ2: 各グループ（品目 × addinfo05）ごとにページング処理
    Object.keys(groupedByItem).forEach(groupKey => {
        const group = groupedByItem[groupKey];
        const itemList = group.items;
        const commonInfo = group.commonInfo;
        const itemcd = commonInfo.itemcd;
        
        const qtyInvTotal = itemList.reduce((s, x) => s + (Number(x.quantity_for_inventory) || Number(x.planned_quantity) || 0), 0);
        const qtyInsTotal = itemList.reduce((s, x) => s + (Number(x.quantity_for_instruction) || Number(x.adjusted_quantity) || 0), 0);
        commonInfo.quantity_for_inventory_total = qtyInvTotal;
        commonInfo.quantity_for_instruction_total = qtyInsTotal;

        // 品目全体の合計値を計算（全ページで使用）
        const itemTotals = calculateItemTotals(itemList);
        
        // 表示用にitemデータを整形
        const processedItems = itemList.map(item => ({
            facility_name: item.shpctrnm || '',
            standard_bags: resolveStandardBags(item),
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
            pageNumber: 1,
            totalPages: 1,
            itemcd: itemcd,
            
            // 品目共通情報
            commonInfo: commonInfo,
            
            // この品目の全施設データ
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
    });
    
    return allPages;
}

/**
 * ラベル用のデータを準備。
 * API POST /api/bagging/calculate（print_type=label）の LabelResponseDto、または従来の袋詰指示書形 items を受ける。
 * @param {Object} apiResponse
 * @returns {Object}
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
