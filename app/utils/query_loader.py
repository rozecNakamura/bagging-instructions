from sqlalchemy.orm import Query, selectinload, InstrumentedAttribute


def load_relations(query: Query, *relations: InstrumentedAttribute) -> Query:
    """
    汎用的なEager Loading関数

    どのモデルでも使用可能。必要なリレーションを可変長引数で指定。

    Args:
        query: SQLAlchemyクエリオブジェクト
        *relations: ロードしたいリレーション属性（可変長引数）

    Returns:
        Eager Loadingオプションを追加したクエリ

    使用例:
        from app.models.jobord import Jobord

        # 基本的な使用
        query = db.query(Jobord)
        query = load_relations(query, Jobord.item, Jobord.shpctr)
        jobords = query.all()

        # 1つだけロード
        query = load_relations(query, Jobord.item)

        # 複数ロード
        query = load_relations(query, Jobord.item, Jobord.shpctr, Jobord.routs)

        # 他のモデルでも同じパターン
        from app.models.item import Item
        query = db.query(Item)
        query = load_relations(query, Item.category, Item.supplier)
    """
    if not relations:
        return query

    options = [selectinload(rel) for rel in relations]
    return query.options(*options)
