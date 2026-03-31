/**
 * 現品票印刷：ブラウザの印刷ダイアログ
 */
import { getProductLabelRows } from './product_label_search.js';

document.getElementById('productLabelPrintBtn').addEventListener('click', () => {
    const rows = getProductLabelRows();
    if (!rows || rows.length === 0) {
        alert('印刷するデータがありません。先に検索してください。');
        return;
    }
    document.body.classList.add('product-label-printing');
    const onAfter = () => {
        document.body.classList.remove('product-label-printing');
        window.removeEventListener('afterprint', onAfter);
    };
    window.addEventListener('afterprint', onAfter);
    window.print();
});
