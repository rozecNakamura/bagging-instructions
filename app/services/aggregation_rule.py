from typing import List, Dict
from app.models.jobord import Jobord

# 集計ルールマッピング
AGGREGATION_RULES = {
    # 院外顧客: 全喫食時間で店舗別集計
    "000": {"morning": "by_facility", "lunch": "by_facility", "dinner": "by_facility"},
    "100": {"morning": "by_facility", "lunch": "by_facility", "dinner": "by_facility"},
    "110": {"morning": "by_facility", "lunch": "by_facility", "dinner": "by_facility"},
    "120": {"morning": "by_facility", "lunch": "by_facility", "dinner": "by_facility"},
    # ケータリング: 朝は店舗別、昼夕は統合
    "200": {"morning": "by_facility", "lunch": "by_catering", "dinner": "by_catering"},
    "210": {"morning": "by_facility", "lunch": "by_catering", "dinner": "by_catering"},
    "240": {"morning": "by_facility", "lunch": "by_catering", "dinner": "by_catering"},
    # 在宅個人: 昼夕のみ統合
    "300": {"morning": None, "lunch": "by_catering", "dinner": "by_catering"},
    "310": {"morning": None, "lunch": "by_catering", "dinner": "by_catering"},
}

EATING_TIME_MAP = {"朝": "morning", "昼": "lunch", "夕": "dinner"}


def get_aggregation_method(cuscd: str, shptm: str) -> str:
    """
    得意先コードと出荷時刻から集計方法を取得

    Args:
        cuscd: 得意先コード
        shptm: 出荷時刻（朝/昼/夕など）

    Returns:
        集計方法（by_facility/by_catering）
    """
    eating_key = EATING_TIME_MAP.get(shptm, "lunch")
    rule = AGGREGATION_RULES.get(cuscd or "", {})
    return rule.get(eating_key, "by_facility")


def apply_aggregation_rule(jobords: List[Jobord]) -> Dict[str, List[Jobord]]:
    """
    集計ルールを適用してグルーピング

    Args:
        jobords: 受注明細リスト

    Returns:
        集計キーごとにグルーピングされた辞書
    """
    grouped = {}

    for jobord in jobords:
        method = get_aggregation_method(jobord.cuscd or "", jobord.shptm or "")

        if method == "by_facility":
            # 店舗別集計
            key = f"{jobord.itemcd}_{jobord.shpctrcd}_{jobord.shptm}"
        else:
            # ケータリング統合
            key = f"{jobord.itemcd}_CATERING_{jobord.shptm}"

        if key not in grouped:
            grouped[key] = []
        grouped[key].append(jobord)

    return grouped
