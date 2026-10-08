"""
ローカルPC操作用スクリプト (local_client_simple.py)
同一PC上で動作している ScanSnapShot に対してキャプチャー実行や状態確認を行います。
"""

import os
import sys
from config import get_base_url, create_session, DEFAULT_API_KEY

def main():
    base_url = get_base_url(host="localhost", port=50050)
    session = create_session(api_key=DEFAULT_API_KEY)

    print("==================================================")
    print(" ScanSnapShot ローカル操作クライアント")
    print(f" 接続先: {base_url}")
    print("==================================================")

    # 1. 状態確認
    try:
        print("\n[1] サーバー状態を確認中...")
        res = session.get(f"{base_url}/api/status")
        if res.status_code == 200:
            status = res.json().get("status", {})
            is_running = status.get("isRunning", False)
            print(f"  -> 接続成功！ 監視稼働状態: {'監視中' if is_running else '待機中'}")
            print(f"  -> 全画面モード: {status.get('isCaptureFullScreen', False)}")
            print(f"  -> 保存先フォルダー: {status.get('saveDirectory', '')}")
        else:
            print(f"  -> エラー: ステータスコード {res.status_code}")
            print(f"  -> レスポンス: {res.text}")
            return
    except requests.exceptions.ConnectionError:
        print("  -> [接続失敗] ScanSnapShot が起動していないか、remote_settings.json が存在しません。")
        print("  -> %APPDATA%\\ScanSnapShot\\remote_settings.json が作成されているか確認してください。")
        return

    # 2. キャプチャー実行
    print("\n[2] キャプチャー撮影を実行します...")
    res = session.post(f"{base_url}/api/capture")
    if res.status_code == 200:
        data = res.json()
        print("  -> キャプチャー撮影成功！")
        print(f"  -> 保存ファイル: {data.get('fileName')}")
        print(f"  -> ファイルパス: {data.get('filePath')}")
    else:
        print(f"  -> キャプチャー失敗: {res.status_code} - {res.text}")

    # 3. キャプチャーフォルダー内の画像一覧・枚数確認
    print("\n[3] 保存先フォルダーの画像一覧を取得中...")
    res = session.get(f"{base_url}/api/images")
    if res.status_code == 200:
        data = res.json()
        print(f"  -> 保存フォルダー: {data.get('saveDirectory')}")
        print(f"  -> 総画像数: {data.get('totalCount')} 枚")
        files = data.get("files", [])
        if files:
            print("  -> 最新の画像 (最大5件):")
            for f in files[:5]:
                print(f"     - {f.get('fileName')} ({f.get('fileSizeBytes'):,} bytes, {f.get('createdAt')})")
    else:
        print(f"  -> 一覧取得失敗: {res.status_code}")

if __name__ == "__main__":
    import requests
    main()
