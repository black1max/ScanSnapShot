"""
ScanSnapShot Python クライアント共通設定モジュール
"""

import urllib3
import requests

# 自己署名証明書の警告（InsecureRequestWarning）を抑制
urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

# --- 接続設定 ---
# ローカルPCで動かす場合: "https://localhost:50050"
# 別PCから動かす場合: "https://192.168.1.xxx:50050" (ScanSnapShotが動作しているPCのIP)
DEFAULT_HOST = "localhost"
DEFAULT_PORT = 50050
DEFAULT_API_KEY = "scansnapshot_secret_key"


def get_base_url(host: str = DEFAULT_HOST, port: int = DEFAULT_PORT) -> str:
    """ベースURLを生成します"""
    return f"https://{host}:{port}"


def get_headers(api_key: str = DEFAULT_API_KEY) -> dict:
    """認証ヘッダーを生成します"""
    return {
        "X-API-KEY": api_key,
        "Accept": "application/json"
    }


def create_session(api_key: str = DEFAULT_API_KEY) -> requests.Session:
    """認証ヘッダーとSSL検証無効を設定済みの requests セッションを作成します"""
    session = requests.Session()
    session.headers.update(get_headers(api_key))
    session.verify = False  # 自己署名証明書のため検証をスキップ
    return session
