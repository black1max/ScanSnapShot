"""
リモート設定変更スクリプト (remote_settings_manager.py)
外部アプリ・別PCから ScanSnapShot の設定（撮影エリア、しきい値、チェック間隔等）を取得・変更します。

使い方:
  python remote_settings_manager.py [相手PCのIP/localhost] [APIキー]
"""

import sys
import json
import requests
from config import get_base_url, create_session, DEFAULT_HOST, DEFAULT_API_KEY

def main():
    host = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_HOST
    api_key = sys.argv[2] if len(sys.argv) > 2 else DEFAULT_API_KEY
    base_url = get_base_url(host=host)
    session = create_session(api_key=api_key)

    print("==================================================")
    print(" ScanSnapShot リモート設定マネージャー")
    print(f" 接続先: {base_url}")
    print("==================================================")

    # 1. 現在の設定を取得
    print("\n[1] 現在の設定を取得します...")
    try:
        res = session.get(f"{base_url}/api/settings", timeout=5)
    except requests.exceptions.RequestException as e:
        print(f"  -> 接続エラー: {e}")
        return

    if res.status_code != 200:
        print(f"  -> 設定取得失敗: {res.status_code} - {res.text}")
        return

    settings = res.json().get("settings", {})
    print(json.dumps(settings, indent=2, ensure_ascii=False))

    # 2. 設定変更のメニュー
    print("\n[2] 設定を変更するサンプル:")
    print("  1: 全画面キャプチャーモードに切り替え (IsCaptureFullScreen: true)")
    print("  2: 指定範囲モードに切り替え (IsCaptureFullScreen: false)")
    print("  3: 監視パラメータを変更 (Interval: 500ms, Sensitivity: 2.0%)")
    print("  4: 変更しない (終了)")

    choice = input("\n選択してください (1-4): ").strip()

    payload = {}
    if choice == "1":
        payload = {"IsCaptureFullScreen": True}
        print("  -> 全画面キャプチャーモードへの変更を送信中...")
    elif choice == "2":
        payload = {"IsCaptureFullScreen": False}
        print("  -> 指定範囲モードへの変更を送信中...")
    elif choice == "3":
        payload = {
            "IntervalMilliseconds": 500,
            "SensitivityThresholdPercent": 2.0,
            "CooldownMilliseconds": 1000
        }
        print("  -> 監視パラメータの変更を送信中...")
    else:
        print("変更を行わずに終了します。")
        return

    # 3. 設定更新リクエストを送信
    update_res = session.post(f"{base_url}/api/settings", json=payload)
    if update_res.status_code == 200:
        print("  -> 設定変更に成功しました！ ScanSnapShot の画面にも即時反映されています。")
    else:
        print(f"  -> 設定変更失敗: {update_res.status_code} - {update_res.text}")

if __name__ == "__main__":
    main()
