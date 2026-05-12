import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

async function importTemplateLoader(relativePath) {
    const source = await readFile(new URL(relativePath, import.meta.url), 'utf8');
    const encoded = Buffer.from(source, 'utf8').toString('base64');
    return import(`data:text/javascript;base64,${encoded}`);
}

function makeInstructionItem(index) {
    return {
        itemcd: 'KIT001',
        itemnm: 'テストキット',
        prddt: '20260512',
        delvedt: '20260513',
        shptm: '1200',
        jobordno: 'JOB001',
        jobordmernm: 'テスト商品',
        shpctrnm: `施設${index + 1}`,
        planned_quantity: 10 + index,
        adjusted_quantity: 10 + index,
        quantity_for_inventory: 10 + index,
        quantity_for_instruction: 10 + index,
        standard_bags: 1,
        irregular_quantity: index % 2,
        seasoning_amounts: [],
        current_stock: 0,
        item: { std: '10', routs: [] },
        shpctr: {},
        mboms: [],
        cusmcd: null,
    };
}

for (const [name, relativePath] of [
    ['static', '../static/js/template_loader.js'],
    ['publish_output', '../publish_output/static/js/template_loader.js'],
]) {
    test(`${name} bagging instruction keeps more than 20 facilities on one printed page`, async () => {
        const { prepareBaggingInstructionData } = await importTemplateLoader(relativePath);
        const items = Array.from({ length: 21 }, (_, index) => makeInstructionItem(index));

        const pages = prepareBaggingInstructionData({ items });

        assert.equal(pages.length, 1);
        assert.equal(pages[0].items.length, 21);
        assert.equal(pages[0].pageNumber, 1);
        assert.equal(pages[0].totalPages, 1);
    });
}
