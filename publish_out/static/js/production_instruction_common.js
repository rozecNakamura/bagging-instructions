/**
 * 調味液配合表 / 生産指示書_ホイコーロー 共通：マルチセレクト要約ラベル
 * @param {number} selectedCount
 * @param {number} masterCount
 * @returns {string}
 */
export function productionInstructionMultiSelectLabel(selectedCount, masterCount) {
    if (selectedCount === 0) return '未選択';
    if (masterCount > 0 && selectedCount === masterCount) return 'すべて選択';
    return `${selectedCount}件選択`;
}
