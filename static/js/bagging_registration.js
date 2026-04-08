/**
 * 袋詰投入量登録・必要量セット・登録・印刷
 */

import {
    getBaggingInput,
    saveBaggingInput,
    fetchBaggingRequiredQuantities,
    calculateBagging,
    normalizePrddt
} from './api.js';
import { generateInstructionPDF, generateLabelPDF } from './pdf_generator.js';
import { getSelectedBaggingGroup } from './search.js';

/** @typedef {{ prddt: string, itemcd: string, itemnm?: string, total_jobordqun: number, unit_name?: string, line_prkeys: number[] }} BaggingSearchGroup */

/** @type {BaggingSearchGroup | null} */
let activeGroup = null;

/** @type {{ input_order: number, citemcd: string, spec_qty: string, total_qty: string }[]} */
let lineEditors = [];

function matchSavedLine(savedLines, reqLine, index) {
    const io = reqLine.input_order != null ? reqLine.input_order : index + 1;
    if (!savedLines?.length) return null;
    return savedLines.find((sl) => {
        if (sl.input_order != null) return sl.input_order === io && sl.citemcd === reqLine.citemcd;
        return sl.citemcd === reqLine.citemcd;
    });
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

        const tdSpec = document.createElement('td');
        const inpSpec = document.createElement('input');
        inpSpec.type = 'number';
        inpSpec.step = 'any';
        inpSpec.className = 'bagging-reg-spec';
        inpSpec.dataset.i = String(i);
        inpSpec.value = line.spec_qty;
        tdSpec.appendChild(inpSpec);

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
        tr.appendChild(tdSpec);
        tr.appendChild(tdTotal);
        tbody.appendChild(tr);
    });

    tbody.querySelectorAll('.bagging-reg-spec').forEach(inp => {
        inp.addEventListener('input', () => {
            const i = +inp.dataset.i;
            if (lineEditors[i]) lineEditors[i].spec_qty = inp.value;
        });
    });
    tbody.querySelectorAll('.bagging-reg-total').forEach(inp => {
        inp.addEventListener('input', () => {
            const i = +inp.dataset.i;
            if (lineEditors[i]) lineEditors[i].total_qty = inp.value;
        });
    });
}

function readParentYieldFromUi() {
    const el = document.getElementById('baggingRegParentYield');
    const v = el?.value?.trim();
    if (v === '' || v == null) return null;
    const n = Number(v);
    return Number.isFinite(n) && n > 0 ? n : null;
}

function buildPayloadFromEditors() {
    const py = readParentYieldFromUi();
    return {
        lines: lineEditors.map(l => ({
            citemcd: l.citemcd,
            input_order: l.input_order,
            spec_qty: l.spec_qty === '' ? null : Number(l.spec_qty),
            total_qty: l.total_qty === '' ? null : Number(l.total_qty)
        })),
        ...(py != null ? { parent_yield_quantity: py } : {})
    };
}

async function loadRegistrationUi(group) {
    activeGroup = group;
    const prodEl = document.getElementById('productionDate');
    const prddt = normalizePrddt(prodEl?.value) || group.prddt;

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
    lineEditors = reqLines.map((reqLine, j) => {
        const io = reqLine.input_order != null ? reqLine.input_order : j + 1;
        const sl = matchSavedLine(savedPayload?.lines, reqLine, j);
        return {
            input_order: io,
            citemcd: reqLine.citemcd || '',
            spec_qty: sl?.spec_qty != null ? String(sl.spec_qty) : '',
            total_qty: sl?.total_qty != null ? String(sl.total_qty) : '0'
        };
    });

    renderLineInputs();
    const pyEl = document.getElementById('baggingRegParentYield');
    if (pyEl) {
        const py = savedPayload?.parent_yield_quantity;
        pyEl.value = py != null && py !== '' ? String(py) : '';
    }
    openSection();
}

document.getElementById('openBaggingRegistrationBtn')?.addEventListener('click', async () => {
    const g = getSelectedBaggingGroup();
    if (!g) {
        alert('袋詰投入量登録を開くには、検索結果で1件だけ選択してください。');
        return;
    }
    try {
        await loadRegistrationUi(g);
    } catch (e) {
        alert('データの取得に失敗しました: ' + e.message);
    }
});

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
        lineEditors = reqLines.map((reqLine, j) => ({
            input_order: reqLine.input_order != null ? reqLine.input_order : j + 1,
            citemcd: reqLine.citemcd || '',
            spec_qty: '',
            total_qty: reqLine.total_qty != null ? String(reqLine.total_qty) : ''
        }));
        renderLineInputs();
    } catch (e) {
        alert('必要量セットに失敗しました: ' + e.message);
    }
});

document.getElementById('baggingRegSaveBtn')?.addEventListener('click', async () => {
    if (!activeGroup) return;
    const prodEl = document.getElementById('productionDate');
    const prddt = normalizePrddt(prodEl?.value) || activeGroup.prddt;
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
    const useSaved = document.getElementById('baggingRegUseSavedInput')?.checked ?? false;
    try {
        const data = await calculateBagging(activeGroup.line_prkeys, printType, useSaved);
        if (printType === 'instruction') {
            generateInstructionPDF(data);
        } else {
            generateLabelPDF(data);
        }
    } catch (e) {
        alert('印刷データの取得に失敗しました: ' + e.message);
    }
});

export { activeGroup, lineEditors };
