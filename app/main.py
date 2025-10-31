from fastapi import FastAPI
from fastapi.staticfiles import StaticFiles
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse
from app.api import search, bagging
from app.core.config import settings

app = FastAPI(
    title="Bagging Instructions System",
    version="1.0.0",
    description="袋詰指示書・ラベル管理システム"
)

# CORS設定
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# APIルーター登録（先に登録）
app.include_router(search.router, prefix="/api", tags=["search"])
app.include_router(bagging.router, prefix="/api/bagging", tags=["bagging"])

@app.get("/health")
def health_check():
    return {"status": "ok", "environment": settings.ENVIRONMENT}

# 静的ファイル配信
app.mount("/static", StaticFiles(directory="static"), name="static")

# ルートパスでindex.htmlを返す
@app.get("/")
def read_root():
    return FileResponse("static/index.html")
