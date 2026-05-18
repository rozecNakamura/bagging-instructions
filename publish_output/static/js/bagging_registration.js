/**
 * 袋詰投入量登録・必要量セット・登録・印刷
 */

import {
    getBaggingInput,
    saveBaggingInput,
    fetchBaggingRequiredQuantities,
    calculateBagging,
    markBaggingPrinted,
    normalizePrddt
} from './api.js';
import { generateInstructionPDF, generateLabelPDF } from './pdf_generator.js';
/** @typedef {{ prddt: string, itemcd: string, itemnm?: string, total_jobordqun: number, unit_name?: string, line_prkeys: number[] }} BaggingSearchGroup */

/** @type {BaggingSearchGroup | null} */
let activeGroup = null;

/** @type {{ input_order: number, citemcd: string, citem_name: string, reference_qty: string, total_qty: string }[]} */
let lineEditors = [];

/** BOM / 必要量行の input_order（1 始まり）。未指定時は行インデックス + 1。 */
function inputOrderForReqLine(reqLine, zeroBasedIndex) {
    return reqLine.input_order != null ? reqLine.input_order : zeroBasedIndex + 1;
}

function matchSavedLine(savedLines, reqLine, index) {
    const io = inputOrderForReqLine(reqLine, index);
    if (!savedLines?.length) return null;
    return savedLines.find((sl) => {
        if (sl.input_order != null) return sl.input_order === io && sl.citemcd === reqLine.citemcd;
        return sl.citemcd === reqLine.citemcd;
    });
}

function prddtFromFormOrGroup(group) {
    const prodEl = document.getElementById('productionDate');
    return normalizePrddt(prodEl?.value) || group.prddt;
}

function getModalRoot() {
    return document.getElementById('baggingRegModal');
}

function openSection() {
    const root = getModalRoot();
    if (root) {
        root.hidden = false;
        root.setAttribute('aria-hidden', 'false');
    }
    document.body.style.overflow = 'hidden';
}

function closeSection() {
    const root = getModalRoot();
    if (root) {
        root.hidden = true;
        root.setAttribute('aria-hidden', 'true');
    }
    document.body.style.overflow = '';
    activeGroup = null;
    lineEditors = [];
    const specInput = document.getElementById('baggingRegSpecQty');
    if (specInput) specInput.value = '';
    const expiryInput = document.getElementById('baggingRegExpiryOverride');
    if (expiryInput) expiryInput.value = '';
}

/** 検索結果クリア時など、他モジュールから呼ぶ用 */
export function closeBaggingRegistration() {
    closeSection();
}

function renderLineInputs() {
    const tbody = document.getElementById('baggingRegLinesBody');
    if (!tbody) return;
    tbody.innerHTML = '';
    lineEditors.forEach((line, i) => {
        const tr = document.createElement('tr');

        const tdOrder = document.createElement('td');
        tdOrder.className = 'bagging-reg-cell-readonly bagging-reg-order-cell';
        tdOrder.textContent = String(line.input_order);

        const tdCd = document.createElement('td');
        tdCd.className = 'bagging-reg-cell-readonly bagging-reg-citemcd-cell';
        tdCd.textContent = line.citemcd;

        const tdNm = document.createElement('td');
        tdNm.className = 'bagging-reg-cell-readonly bagging-reg-citemnm-cell';
        tdNm.textContent = line.citem_name || '';

        const tdReference = document.createElement('td');
        tdReference.className = 'bagging-reg-cell-readonly bagging-reg-reference-cell';
        tdReference.textContent = line.reference_qty || '';

        const tdTotal = document.createElement('td');
        const inpTotal = document.createElement('input');
        inpTotal.type = 'number';
        inpTotal.step = 'any';
        inpTotal.className = 'bagging-reg-total';
        inpTotal.dataset.i = String(i);
        inpTotal.value = line.total_qty;
        tdTotal.appendChild(inpTotal);

        tr.appendChild(tdOrder);
        tr.appendChild(tdCd);
        tr.appendChild(tdNm);
        tr.appendChild(tdReference);
        tr.appendChild(tdTotal);
        tbody.appendChild(tr);
    });

    tbody.querySelectorAll('.bagging-reg-total').forEach(inp => {
        inp.addEventListener('input', () => {
            const i = +inp.dataset.i;
            if (lineEditors[i]) lineEditors[i].total_qty = inp.value;
        });
    });
}

function buildPayloadFromEditors() {
    const specVal = document.getElementById('baggingRegSpecQty')?.value;
    const specQty = specVal == null || specVal === '' ? null : Number(specVal);
    return {
        lines: lineEditors.map(l => ({
            citemcd: l.citemcd,
            input_order: l.input_order,
            spec_qty: specQty,
            total_qty: l.total_qty === '' ? null : Number(l.total_qty)
        }))
    };
}

async function loadRegistrationUi(group) {
    activeGroup = group;
    const prddt = prddtFromFormOrGroup(group);
    const expiryInput = document.getElementById('baggingRegExpiryOverride');
    if (expiryInput) expiryInput.value = '';

    const ctx = document.getElementById('baggingRegContext');
    if (ctx) {
        const u = group.unit_name || group.unit_code || '';
        ctx.textContent = `製造日: ${group.prddt}  品目: ${group.itemcd} ${group.itemnm || ''}  合計受注: ${group.total_jobordqun} ${u}`.trim();
    }

    const required = await fetchBaggingRequiredQuantities(group.line_prkeys);
    let savedPayload = null;
    try {
        const saved = await getBaggingInput(prddt, group.itemcd, group.line_prkeys);
        savedPayload = saved.payload;
    } catch {
        savedPayload = null;
    }

    const reqLines = required.lines || [];

    // 規格数量は親品目共通の1値 — 保存済み→選択品目STDの順で取得
    const firstSavedSpec = savedPayload?.lines?.find(sl => sl.spec_qty != null)?.spec_qty;
    const firstDefaultSpec = reqLines[0]?.spec_qty;
    const resolvedSpec = firstSavedSpec ?? firstDefaultSpec ?? '';
    const specInput = document.getElementById('baggingRegSpecQty');
    if (specInput) specInput.value = resolvedSpec !== '' ? String(resolvedSpec) : '';

    lineEditors = reqLines.map((reqLine, j) => {
        const io = inputOrderForReqLine(reqLine, j);
        const sl = matchSavedLine(savedPayload?.lines, reqLine, j);
        return {
            input_order: io,
            citemcd: reqLine.citemcd || '',
            citem_name: reqLine.citem_name || '',
            reference_qty: reqLine.reference_qty != null ? String(reqLine.reference_qty) : '',
            total_qty: sl?.total_qty != null ? String(sl.total_qty) : (reqLine.total_qty != null ? String(reqLine.total_qty) : '0')
        };
    });

    renderLineInputs();
    openSection();
}

/**
 * 検索結果行クリックから呼ぶ。投入量登録モーダルを開きデータを読み込む。
 * @param {BaggingSearchGroup} group
 */
export async function openBaggingRegistrationForGroup(group) {
    if (!group?.line_prkeys?.length) {
        alert('この行には受注明細がありません。');
        return;
    }
    try {
        await loadRegistrationUi(group);
    } catch (e) {
        alert('データの取得に失敗しました: ' + e.message);
    }
}

document.getElementById('baggingRegCloseBtn')?.addEventListener('click', () => closeSection());

document.getElementById('baggingRegModalBackdrop')?.addEventListener('click', () => closeSection());

document.addEventListener('keydown', (e) => {
    if (e.key !== 'Escape') return;
    const root = getModalRoot();
    if (root && !root.hidden) closeSection();
});

document.getElementById('baggingRegRequiredBtn')?.addEventListener('click', async () => {
    if (!activeGroup) return;
    try {
        const required = await fetchBaggingRequiredQuantities(activeGroup.line_prkeys);
        const reqLines = required.lines || [];
        const specInput = document.getElementById('baggingRegSpecQty');
        if (specInput && reqLines[0]?.spec_qty != null) specInput.value = String(reqLines[0].spec_qty);
        lineEditors = reqLines.map((reqLine, j) => ({
            input_order: inputOrderForReqLine(reqLine, j),
            citemcd: reqLine.citemcd || '',
            citem_name: reqLine.citem_name || '',
            reference_qty: reqLine.reference_qty != null ? String(reqLine.reference_qty) : '',
            total_qty: reqLine.total_qty != null ? String(reqLine.total_qty) : ''
        }));
        renderLineInputs();
    } catch (e) {
        alert('必要量セットに失敗しました: ' + e.message);
    }
});

document.getElementById('baggingRegSaveBtn')?.addEventListener('click', async () => {
    if (!activeGroup) return;
    const prddt = prddtFromFormOrGroup(activeGroup);
    try {
        await saveBaggingInput(prddt, activeGroup.itemcd, buildPayloadFromEditors(), activeGroup.line_prkeys);
        alert('登録しました。');
    } catch (e) {
        alert('登録に失敗しました: ' + e.message);
    }
});

document.getElementById('baggingRegPrintBtn')?.addEventListener('click', async () => {
    if (!activeGroup) return;
    const printType = document.querySelector('input[name="baggingRegPrintType"]:checked')?.value || 'instruction';
    const expiryInput = document.getElementById('baggingRegExpiryOverride');
    const expiryOverride = expiryInput?.value ? expiryInput.value.replace(/-/g, '') : null;
    try {
        const prddt = prddtFromFormOrGroup(activeGroup);
        await saveBaggingInput(prddt, activeGroup.itemcd, buildPayloadFromEditors(), activeGroup.line_prkeys);
        const data = await calculateBagging(activeGroup.line_prkeys, printType, true, expiryOverride);
        if (printType === 'label') {
            await generateLabelPDF(data);
        } else {
            await generateInstructionPDF(data);
        }
        // 印刷済みフラグをセットし検索結果を更新
        const printedPrddt = prddtFromFormOrGroup(activeGroup);
        try {
            await markBaggingPrinted(printedPrddt, activeGroup.itemcd, printType);
            const { refreshBaggingSearch } = await import('./search.js');
            await refreshBaggingSearch();
        } catch { /* 印刷自体は成功しているので無視 */ }
    } catch (e) {
        alert('印刷データの取得に失敗しました: ' + e.message);
    }
});

export { activeGroup, lineEditors };
