from sqlalchemy import create_engine
from sqlalchemy.ext.declarative import declarative_base
from sqlalchemy.orm import sessionmaker
from app.core.config import settings

# SQLAlchemyエンジン作成
engine = create_engine(
    settings.DATABASE_URL,
    pool_pre_ping=True,  # 接続チェック
    echo=False,  # SQLログを無効化
)

# セッションファクトリ
SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)

# ベースクラス
Base = declarative_base()


# 依存性注入用のDB取得関数
def get_db():
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()
