/**
 * 納品書：PDF印刷
 */
import { getSelectedDeliveryNoteRows } from './delivery_note_search.js';
import { generateDeliveryNotePDF } from './pdf_generator.js';

document.getElementById('deliveryNotePrintBtn').addEventListener('click', async () => {
    const rows = getSelectedDeliveryNoteRows();
    if (rows.length === 0) {
        alert('印刷する項目を選択してください');
        return;
    }
    try {
        await generateDeliveryNotePDF(rows);
    } catch (error) {
        alert('印刷に失敗しました: ' + error.message);
    }
});
