import math
from typing import List, Dict, Any
from app.models.item import Item
from app.models.mbom import Mbom


def resolve_parent_divisor(item: Item | None) -> float:
    """親の規格除数（std1→std3→std→car、なければ 1）。C# BaggingDivisorResolver と同系。"""
    divisor = None

    if item is not None:
        for attr in ("std1", "std2", "std3", "std"):
            val = getattr(item, attr, None)
            if not val:
                continue
            try:
                x = float(val)
                if x > 0:
                    divisor = x
                    break
            except (ValueError, TypeError):
                pass

    if divisor is None and item is not None:
        if hasattr(item, "car") and item.car:
            try:
                car = float(item.car)
                if car > 0:
                    divisor = car
            except (ValueError, TypeError):
                pass

    if divisor is None:
        divisor = 1.0
    return float(divisor)


def round_up_quantity_with_seasoning(
    jobordqun: float, item: Item | None, mboms: List[Mbom] = None
) -> tuple[float, float, List[Dict[str, Any]]]:
    """
    個数単位の品目で、受注量を親品目の規格で割った余りがある場合は切り上げ、
    子部品の単位に応じて切り上げ処理または調味液計算を実施
    """
    mboms = mboms or []

    # mboms が空の場合はスキップ
    if not mboms:
        return (jobordqun, 0.0, [])

    # ============================================================
    # 1. 親品目の規格（std1→std2→std3、互換で std、なければ CAR）
    # ============================================================
    divisor = resolve_parent_divisor(item)

    # ============================================================
    # 2. 受注数の計算: JOBORDQUN / (STD or CAR) = 整数部分 + 余り
    # ============================================================
    # 整数除算で整数部分を取得
    integer_part = int(jobordqun // divisor)

    # 剰余演算子で実際の余りを計算
    actual_remainder = jobordqun % divisor

    seasoning_list = []

    # 浮動小数点の誤差を考慮（非常に小さい値は0とみなす）
    if actual_remainder > 1e-10:  # 0.0000000001より大きい場合
        # ============================================================
        # 3. 余りがある場合、端数処理（余りを切り上げ）
        # ============================================================
        rounded_remainder = math.ceil(actual_remainder)

        # ============================================================
        # 4. 子部品情報を作成（単位で判定）
        # ============================================================
        seasoning_list = []
        
        # 全子部品をループして、各々の単位で判定
        for mbom in mboms:
            # 子部品の単位を取得
            child_unit_name = None
            if hasattr(mbom, "child_item") and mbom.child_item:
                if hasattr(mbom.child_item, "uni") and mbom.child_item.uni:
                    child_unit_name = getattr(mbom.child_item.uni, "uninm", None)
            
            # 個数単位かどうかで処理を分岐
            if child_unit_name in ["個", "ケ", "ヶ", "箇", "コ"]:
                # 個数単位: 切り上げた余りをそのまま使用
                calculated_amount = rounded_remainder
            else:
                # それ以外: 調味液計算
                otp = float(mbom.otp) if mbom.otp else 0.0
                amu = float(mbom.amu) if mbom.amu else 0.0
                if otp == 0:
                    calculated_amount = 0.0
                else:
                    calculated_amount = (rounded_remainder / otp) * amu
            
            # _create_seasoning_info を使用して情報を作成
            mbom_info = _create_seasoning_info(mbom, calculated_amount)
            seasoning_list.append(mbom_info)

        return (float(integer_part), float(rounded_remainder), seasoning_list)
    else:
        # 余りが0の場合でも、子部品情報を作成（0.00として表示するため）
        seasoning_list = []
        for mbom in mboms:
            mbom_info = _create_seasoning_info(mbom, 0.0)
            seasoning_list.append(mbom_info)
        
        return (float(integer_part), 0.0, seasoning_list)


def _create_seasoning_info(mbom: Mbom, calculated_amount: float) -> Dict[str, Any]:
    """
    mbom情報から調味液情報辞書を作成
    """
    otp = float(mbom.otp) if mbom.otp else 0.0
    amu = float(mbom.amu) if mbom.amu else 0.0
    
    # child_item を辞書形式に変換（シリアライズ可能にする）
    child_item_dict = None
    if hasattr(mbom, "child_item") and mbom.child_item:
        child_item = mbom.child_item

        # uni情報の変換
        uni_dict = None
        if hasattr(child_item, "uni") and child_item.uni:
            uni_dict = {
                "uninm": child_item.uni.uninm
                if hasattr(child_item.uni, "uninm")
                else None
            }

        # routs情報の変換（最初の工程のみ）
        routs_list = []
        if hasattr(child_item, "routs") and child_item.routs:
            for rout in child_item.routs:
                # workc情報の変換
                workc_dict = None
                if hasattr(rout, "workc") and rout.workc:
                    workc_dict = {
                        "wcnm": rout.workc.wcnm
                        if hasattr(rout.workc, "wcnm")
                        else None
                    }

                # ware情報の変換
                ware_dict = None
                if hasattr(rout, "ware") and rout.ware:
                    ware_dict = {
                        "whnm": rout.ware.whnm
                        if hasattr(rout.ware, "whnm")
                        else None
                    }

                routs_list.append({"workc": workc_dict, "ware": ware_dict})

        child_item_dict = {
            "itemcd": child_item.itemcd if hasattr(child_item, "itemcd") else None,
            "itemnm": child_item.itemnm if hasattr(child_item, "itemnm") else None,
            "uni": uni_dict,
            "routs": routs_list,
        }

    return {
        "citemcd": mbom.citemcd,
        "citemgr": mbom.citemgr,
        "cfctcd": mbom.cfctcd,
        "cdeptcd": mbom.cdeptcd,
        "amu": amu,
        "otp": otp,
        "calculated_amount": calculated_amount,
        "child_item": child_item_dict,
    }
