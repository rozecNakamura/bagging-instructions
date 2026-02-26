/**
 * PDF生成処理（ブラウザ印刷プレビュー）
 */

import { loadTemplate, injectData, prepareBaggingInstructionData, prepareLabelData, injectLabelData } from './template_loader.js';
import { generateJuicePdfBlob } from './api.js';

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
        const templateHtml = await loadTemplate('/static/templates/bagging_instruction.html');
        
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
    // APIレスポンスをラベル用データに変換
    const labelData = prepareLabelData(data);
    
    // 印刷プレビュー表示
    await showPrintPreview(
        '/static/templates/label.html',
        labelData,
        injectLabelData
    );
}

/**
 * 汁仕分表の PDF を表示し、ブラウザの印刷プレビュー（ポップアップ）を開く。
 * 袋詰指示書と同様に、印刷ダイアログが前面に表示される。
 * @param {{ delvedt: string, shptmDisplay: string, jobordmernm: string, shpctrnm: string, jobordqun: number, addinfo02: string }[]} rows - 選択された行データ
 */
export async function generateJuicePDF(rows) {
    if (!rows || rows.length === 0) return;
    const blob = await generateJuicePdfBlob(rows);
    const url = URL.createObjectURL(blob);
    const iframe = document.createElement('iframe');
    iframe.style.cssText = 'position:absolute;width:0;height:0;border:0;visibility:hidden';
    iframe.title = '汁仕分表 PDF 印刷';
    iframe.onload = () => {
        try {
            if (iframe.contentWindow) iframe.contentWindow.print();
        } catch (_) {
            // 同一オリジンでない場合は新しいタブで開く
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

function escapeHtml(text) {
    if (text == null) return '';
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

