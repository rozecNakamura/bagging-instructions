import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';

const rowCount = Number(process.argv[2] || 21);
const outputDir = path.resolve('artifacts', 'print-probe');
const templatePath = path.resolve('static', 'templates', 'bagging_instruction.html');
const outputPath = path.join(outputDir, `bagging-${rowCount}-rows.html`);

function mainTableSizing(itemCount) {
    const defaultRowHeight = 22;
    const defaultBodyRowsThatFit = 26;
    const bodyRows = Math.max(21, itemCount + 1);
    const rowHeight = Math.max(
        12,
        Math.min(defaultRowHeight, Math.floor((defaultBodyRowsThatFit * defaultRowHeight) / bodyRows)),
    );

    return {
        rowHeight,
        cellFontSize: Math.max(7, Math.min(11, rowHeight - 7)),
        irregularFontSize: Math.max(6, Math.min(9, rowHeight - 9)),
        lineHeight: Math.max(8, rowHeight - 8),
    };
}

function cell(value = '&nbsp;', left = false) {
    const justify = left ? ' style="justify-content: flex-start;"' : '';
    return `<td><div class="cell-content"${justify}>${value}</div></td>`;
}

function row(index, total = false) {
    if (total) {
        const totalCells = [cell('合計', true), cell('999')];
        for (let i = 0; i < 10; i++) totalCells.push(cell('999.99'));
        totalCells.push(cell('99.99'));
        return `<tr class="total-row">${totalCells.join('')}</tr>`;
    }

    const cells = [cell(`施設${index + 1}`, true), cell('1')];
    for (let i = 0; i < 10; i++) cells.push(cell('12.34'));
    cells.push(cell('5'));
    return `<tr>${cells.join('')}</tr>`;
}

const template = await readFile(templatePath, 'utf8');
const rows = Array.from({ length: rowCount }, (_, i) => row(i));
rows.push(row(rowCount, true));
const sizing = mainTableSizing(rowCount);
const mainTableStyle = [
    `--bagging-main-row-height: ${sizing.rowHeight}px`,
    `--bagging-main-cell-font-size: ${sizing.cellFontSize}px`,
    `--bagging-irregular-font-size: ${sizing.irregularFontSize}px`,
    `--bagging-main-line-height: ${sizing.lineHeight}px`,
].join('; ');

const html = template
    .replace('<table class="main-table">', `<table class="main-table" style="${mainTableStyle}">`)
    .replace(
        /<tbody id="facilityTableBody">[\s\S]*?<\/tbody>/,
        `<tbody id="facilityTableBody">\n${rows.join('\n')}\n                </tbody>`,
    )
    .replace(/<td id="kitName"><\/td>/, '<td id="kitName">印刷高さ確認用テスト商品</td>');

await mkdir(outputDir, { recursive: true });
await writeFile(outputPath, html, 'utf8');
console.log(outputPath);
