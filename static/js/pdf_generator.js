/**
 * PDF生成処理
 */

/**
 * 袋詰指示書PDFを生成
 */
export function generateInstructionPDF(data) {
    const { jsPDF } = window.jspdf;
    const doc = new jsPDF();
    
    // タイトル
    doc.setFontSize(18);
    doc.text('袋詰指示書', 105, 15, { align: 'center' });
    
    // テーブルデータ準備
    const tableData = data.items.map(item => [
        item.facility_name,
        item.product_name,
        item.eating_date,
        item.eating_time,
        item.planned_quantity.toFixed(2),
        item.adjusted_quantity.toFixed(2),
        item.standard_bags,
        item.irregular_quantity.toFixed(2)
    ]);
    
    // テーブル生成
    doc.autoTable({
        startY: 25,
        head: [['施設名', '品目名', '喫食日', '喫食時間', '計画量', '調整後数量', '規格袋数', '端数']],
        body: tableData,
        theme: 'grid',
        styles: { font: 'helvetica', fontSize: 10 },
        headStyles: { fillColor: [66, 139, 202] }
    });
    
    // PDF出力
    doc.save('袋詰指示書.pdf');
}

/**
 * ラベルPDFを生成
 */
export function generateLabelPDF(data) {
    const { jsPDF } = window.jspdf;
    const doc = new jsPDF();
    
    let yPosition = 20;
    const labelHeight = 50;
    const pageHeight = 280;
    
    data.items.forEach((item, index) => {
        // ページ送り
        if (yPosition + labelHeight > pageHeight) {
            doc.addPage();
            yPosition = 20;
        }
        
        // ラベル枠
        doc.rect(10, yPosition, 190, labelHeight);
        
        // ラベル内容
        doc.setFontSize(12);
        doc.text(`品目: ${item.product_name} (${item.product_code})`, 15, yPosition + 10);
        doc.text(`喫食日: ${item.eating_date} ${item.eating_time}`, 15, yPosition + 20);
        doc.text(`賞味期限: ${item.expiry_date || '-'}`, 15, yPosition + 30);
        
        if (item.label_type === 'standard') {
            doc.text(`規格量: ${item.standard_quantity}`, 15, yPosition + 40);
        } else {
            doc.text(`施設: ${item.facility_name}`, 15, yPosition + 40);
            doc.text(`端数: ${item.irregular_quantity}`, 100, yPosition + 40);
        }
        
        yPosition += labelHeight + 10;
    });
    
    // PDF出力
    doc.save('ラベル.pdf');
}

