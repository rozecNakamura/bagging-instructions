/**
 * PDF生成処理（ブラウザ印刷プレビュー）
 */

import { loadTemplate, injectData, prepareBaggingInstructionData, prepareLabelData, injectLabelData } from './template_loader.js';
import { generateJuicePdfBlob, generateBentoPdfBlob, generateGohanPdfBlob, generateDeliveryNotePdfBlob, generatePersonalDeliveryPdfBlob, generateBaggingLabelPdfBlob } from './api.js';

function baggingTemplateUrl(fileName) {
    const base =
        typeof window !== 'undefined' && window.__STATIC_BASE__
            ? String(window.__STATIC_BASE__).replace(/\/+$/, '')
            : '/static';
    return `${base}/templates/${fileName}`;
}

/**
 * 印刷プレビューを表示
 * @param {string} templatePath - テンプレートHTMLファイルのパス
 * @param {Object} data - テンプレートに注入するデータ
 * @param {Function} injectFunction - データ注入関数（オプション、デフォルトはinjectData）
 */
async function showPrintPreview(templatePath, data, injectFunction = injectData) {
    try {
        // テンプレートを読み込み
        const templateHtml = await loadTemplate(templatePath);
        
        // データを注入
        const element = injectFunction(templateHtml, data);
        
        // 既存の印刷用コンテナがあれば削除
        const existingContainer = document.getElementById('print-preview-container');
        if (existingContainer) {
            document.body.removeChild(existingContainer);
        }
        
        // 印刷用のコンテナを作成
        const printContainer = document.createElement('div');
        printContainer.id = 'print-preview-container';
        printContainer.style.display = 'none'; // 画面には表示しない
        printContainer.appendChild(element);
        document.body.appendChild(printContainer);
        
        // 少し待ってからブラウザの印刷ダイアログを表示
        await new Promise(resolve => setTimeout(resolve, 100));
        
        // 印刷プレビューを表示
        window.print();
        
        // 印刷ダイアログが閉じられた後に要素を削除
        // （印刷完了を待つために少し遅延）
        setTimeout(() => {
            const container = document.getElementById('print-preview-container');
            if (container && container.parentNode) {
                document.body.removeChild(container);
            }
        }, 1000);
        
    } catch (error) {
        console.error('印刷プレビュー表示エラー:', error);
        console.error('Error stack:', error.stack);
        throw error;
    }
}

/**
 * 袋詰指示書の印刷プレビューを表示（複数ページ対応）
 * @param {Object} data - 袋詰指示書データ（APIレスポンス）
 */
export async function generateInstructionPDF(data) {
    // APIレスポンスをページデータ配列に変換
    const pages = prepareBaggingInstructionData(data);
    
    try {
        // テンプレートを読み込み
        const templateHtml = await loadTemplate(baggingTemplateUrl('bagging_instruction.html'));
        
        // 既存の印刷用コンテナがあれば削除
        const existingContainer = document.getElementById('print-preview-container');
        if (existingContainer) {
            document.body.removeChild(existingContainer);
        }
        
        // 全ページのHTML要素を生成
        const printContainer = document.createElement('div');
        printContainer.id = 'print-preview-container';
        printContainer.style.display = 'none';
        
        pages.forEach((pageData, index) => {
            // 各ページのデータを注入
            const pageElement = injectData(templateHtml, pageData);
            
            // 最後のページ以外にはpage-breakを追加
            if (index < pages.length - 1) {
                pageElement.style.pageBreakAfter = 'always';
            }
            
            printContainer.appendChild(pageElement);
        });
        
        document.body.appendChild(printContainer);
        
        // 少し待ってから印刷ダイアログを表示
        await new Promise(resolve => setTimeout(resolve, 100));
        window.print();
        
        // 印刷ダイアログが閉じられた後に要素を削除
        setTimeout(() => {
            const container = document.getElementById('print-preview-container');
            if (container && container.parentNode) {
                document.body.removeChild(container);
            }
        }, 1000);
        
    } catch (error) {
        console.error('印刷プレビュー表示エラー:', error);
        console.error('Error stack:', error.stack);
        throw error;
    }
}

/**
 * ラベルの印刷プレビューを表示
 * @param {Object} data - ラベルデータ（APIレスポンス）
 */
export async function generateLabelPDF(data) {
    const items = data.items || [];
    if (items.length === 0) throw new Error('ラベルデータがありません');
    const blob = await generateBaggingLabelPdfBlob(items);
    openLabelPdfForPrint(blob, '袋詰現品票 PDF 印刷');
}

/**
 * 汁仕分表の PDF を表示し、ブラウザの印刷プレビュー（ポップアップ）を開く。
 * 袋詰指示書と同様に、印刷ダイアログが前面に表示される。
 * @param {{ delvedt: string, shptmDisplay: string, jobordmernm: string, shpctrnm: string, jobordqun: number, addinfo01: string }[]} rows - 選択された行データ
 */
export async function generateJuicePDF(rows) {
    if (!rows || rows.length === 0) return;
    const blob = await generateJuicePdfBlob(rows);
    openPdfInIframe(blob, '汁仕分表 PDF 印刷');
}

/**
 * 弁当箱盛り付け指示書の PDF を表示し、ブラウザの印刷プレビューを開く。
 * @param {Array} rows
 * @param {string} bentoType okazu | gohan
 */
export async function generateBentoPDF(rows, bentoType = 'okazu') {
    if (!rows || rows.length === 0) return;
    const blob = await generateBentoPdfBlob(rows, bentoType);
    const title = bentoType === 'gohan'
        ? '弁当箱盛り付け指示書（ご飯） PDF 印刷'
        : '弁当箱盛り付け指示書（おかず） PDF 印刷';
    openPdfInIframe(blob, title);
}

/**
 * ご飯盛り付け指示書の PDF を表示し、ブラウザの印刷プレビューを開く。
 */
export async function generateGohanPDF(rows) {
    if (!rows || rows.length === 0) return;
    const blob = await generateGohanPdfBlob(rows);
    openPdfInIframe(blob, 'ご飯盛り付け指示書 PDF 印刷');
}

/**
 * 納品書の PDF を表示し、ブラウザの印刷プレビューを開く。
 * 納品書.rxz テンプレートでサーバー側 PDF 生成。
 * @param {{ eating_date: string, location_code: string, customer_code: string }[]} rows - 選択された行データ
 */
export async function generateDeliveryNotePDF(rows) {
    if (!rows || rows.length === 0) return;
    const blob = await generateDeliveryNotePdfBlob(rows);
    openPdfInIframe(blob, '納品書 PDF 印刷');
}

/**
 * 個人配送指示書（明細）の PDF を表示し、ブラウザの印刷プレビューを開く。個人配送指示書.rxz のみ。
 * @param {{ delivery_date: string, time_name: string, area: string }[]} rows - 選択された行データ
 */
export async function generatePersonalDeliveryDetailPDF(rows) {
    if (!rows || rows.length === 0) return;
    const blob = await generatePersonalDeliveryPdfBlob(rows, 'detail');
    openPdfInIframe(blob, '個人配送指示書 PDF 印刷');
}

/**
 * 個人配送指示書（集計）の PDF を表示し、ブラウザの印刷プレビューを開く。個人配送指示書（集計）.rxz のみ。
 * @param {{ delivery_date: string, time_name: string, area: string }[]} rows - 選択された行データ
 */
export async function generatePersonalDeliverySummaryPDF(rows) {
    if (!rows || rows.length === 0) return;
    const blob = await generatePersonalDeliveryPdfBlob(rows, 'summary');
    openPdfInIframe(blob, '個人配送指示書（集計） PDF 印刷');
}

export function openPdfInIframe(blob, title) {
    const url = URL.createObjectURL(blob);
    const iframe = document.createElement('iframe');
    iframe.style.cssText = 'position:absolute;width:0;height:0;border:0;visibility:hidden';
    iframe.title = title;
    iframe.onload = () => {
        try {
            if (iframe.contentWindow) iframe.contentWindow.print();
        } catch (_) {
            window.open(url, '_blank', 'noopener');
        }
        setTimeout(() => {
            document.body.removeChild(iframe);
            URL.revokeObjectURL(url);
        }, 60000);
    };
    iframe.src = url;
    document.body.appendChild(iframe);
}

/**
 * ラベル印刷専用（現品票など小サイズ PDF）。
 * 通常の openPdfInIframe では Chrome が 0×0 iframe から正確な用紙サイズを読み取れず
 * A4 扱いにしてしまう場合があるため、PDF 本来のサイズで印刷できるよう
 * 新しいウィンドウで PDF を表示し、1.5 秒後に印刷ダイアログを自動表示する。
 *
 * onload イベントは Chrome PDF ビューワーで複数回発火するため、
 * setTimeout によるワンショット呼び出しで印刷ダイアログの二重表示を防ぐ。
 *
 * 印刷時は用紙サイズを「60×60mm」、倍率を「実際のサイズ」に設定してください。
 *
 * @param {Blob} blob - PDF blob
 * @param {string} _title - （将来利用のため残置）
 */
export function openLabelPdfForPrint(blob, _title) {
    const url = URL.createObjectURL(blob);
    const labelPx = Math.ceil(60 * 96 / 25.4); // 60mm ≈ 227px @96dpi
    const win = window.open(url, '_blank',
        `noopener,width=${labelPx * 4},height=${labelPx * 4 + 60},toolbar=1,menubar=0,scrollbars=1`);
    // ポップアップブロック時はフォールバック
    if (!win) {
        openPdfInIframe(blob, _title);
        URL.revokeObjectURL(url);
        return;
    }
    // PDF 読み込み後に1回だけ印刷ダイアログを自動表示。
    // onload は Chrome PDF ビューワーで複数回発火するため setTimeout で代替し、
    // 印刷ダイアログの二重表示を防ぐ。
    setTimeout(() => {
        try { if (!win.closed) win.print(); } catch (_) {}
    }, 1500);
    // URL の解放：ウィンドウが閉じられるか2分後に解放
    setTimeout(() => URL.revokeObjectURL(url), 120000);
}

function escapeHtml(text) {
    if (text == null) return '';
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

