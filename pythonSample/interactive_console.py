"""
対話式オールインワンクライアント (interactive_console.py)
ローカルPCまたは別PCから ScanSnapShot のすべての機能（キャプチャー、ファイル一覧、ダウンロード、監視制御、設定更新）を
メニュー選択で手軽に操作できるツールです。

使い方:
  python interactive_console.py [相手PCのIP/localhost] [ポート番号] [APIキー]
"""

import os
import sys
import json
import requests
from config import create_session, DEFAULT_HOST, DEFAULT_PORT, DEFAULT_API_KEY

def print_menu():
    print("\n------------------- 操作メニュー -------------------")
    print(" 1. 状態確認 (Status)")
    print(" 2. 今すぐキャプチャー (手元のPCにPNGダウンロード保存)")
    print(" 3. 保存フォルダーの画像一覧 & 枚数確認 (Images List)")
    print(" 4. 過去の画像を個別ダウンロード (Download specific image)")
    print(" 5. 現在の設定を表示 (Settings)")
    print(" 6. 監視を開始する (Start Monitor)")
    print(" 7. 監視を停止する (Stop Monitor)")
    print(" 0. 終了")
    print("----------------------------------------------------")

def main():
    host = sys.argv[1] if len(sys.argv) > 1 else input("接続先ホスト/IP [デフォルト: localhost]: ").strip() or DEFAULT_HOST
    port = int(sys.argv[2]) if len(sys.argv) > 2 else DEFAULT_PORT
    api_key = sys.argv[3] if len(sys.argv) > 3 else DEFAULT_API_KEY

    base_url = f"https://{host}:{port}"
    session = create_session(api_key=api_key)

    download_dir = os.path.join(os.path.dirname(__file__), "downloaded_images")
    os.makedirs(download_dir, exist_ok=True)

    print("==================================================")
    print(" ScanSnapShot 対話式コンソール")
    print(f" 接続先: {base_url}")
    print(f" 画像保存先: {download_dir}")
    print("==================================================")

    while True:
        print_menu()
        choice = input("操作番号を入力してください: ").strip()

        if choice == "0":
            print("終了します。")
            break

        try:
            if choice == "1":
                # 状態確認
                res = session.get(f"{base_url}/api/status", timeout=5)
                if res.status_code == 200:
                    status = res.json().get("status", {})
                    print(json.dumps(status, indent=2, ensure_ascii=False))
                else:
                    print(f"エラー: {res.status_code} - {res.text}")

            elif choice == "2":
                # 今すぐキャプチャー＆ダウンロード
                print("キャプチャーを実行中...")
                res = session.post(f"{base_url}/api/capture?download=true", timeout=10)
                if res.status_code == 200:
                    # Content-Disposition からファイル名を取得または生成
                    filename = "captured.png"
                    disp = res.headers.get("Content-Disposition", "")
                    if "filename=" in disp:
                        filename = disp.split("filename=")[-1].strip('"\'')
                    else:
                        from datetime import datetime
                        filename = f"capture_{datetime.now().strftime('%Y%m%d_%H%M%S')}.png"

                    save_path = os.path.join(download_dir, filename)
                    with open(save_path, "wb") as f:
                        f.write(res.content)
                    print(f"撮影成功！ 手元に保存しました: {save_path} ({len(res.content):,} bytes)")
                else:
                    print(f"キャプチャー失敗: {res.status_code} - {res.text}")

            elif choice == "3":
                # 画像一覧＆枚数
                res = session.get(f"{base_url}/api/images", timeout=5)
                if res.status_code == 200:
                    data = res.json()
                    print(f"フォルダー: {data.get('saveDirectory')}")
                    print(f"総枚数: {data.get('totalCount')} 枚")
                    files = data.get("files", [])
                    for i, f in enumerate(files[:15], 1):
                        print(f"  [{i:2d}] {f['fileName']} ({f['fileSizeBytes']:,} bytes, {f['createdAt']})")
                    if len(files) > 15:
                        print(f"  ... 他 {len(files) - 15} 件")
                else:
                    print(f"一覧取得失敗: {res.status_code} - {res.text}")

            elif choice == "4":
                # 任意ファイルダウンロード
                target_file = input("ダウンロードしたいファイル名 (例: Snap_2026_...png): ").strip()
                if not target_file:
                    continue
                res = session.get(f"{base_url}/api/images/{target_file}", timeout=10)
                if res.status_code == 200:
                    save_path = os.path.join(download_dir, target_file)
                    with open(save_path, "wb") as f:
                        f.write(res.content)
                    print(f"ダウンロード成功: {save_path}")
                else:
                    print(f"ダウンロード失敗: {res.status_code} - {res.text}")

            elif choice == "5":
                # 設定表示
                res = session.get(f"{base_url}/api/settings", timeout=5)
                if res.status_code == 200:
                    settings = res.json().get("settings", {})
                    print(json.dumps(settings, indent=2, ensure_ascii=False))
                else:
                    print(f"設定取得失敗: {res.status_code} - {res.text}")

            elif choice == "6":
                # 監視開始
                res = session.post(f"{base_url}/api/monitor/start", timeout=5)
                if res.status_code == 200:
                    print("自動監視を開始しました（トレイ格納）。")
                else:
                    print(f"監視開始失敗: {res.status_code} - {res.text}")

            elif choice == "7":
                # 監視停止
                res = session.post(f"{base_url}/api/monitor/stop", timeout=5)
                if res.status_code == 200:
                    print("自動監視を停止しました。")
                else:
                    print(f"監視停止失敗: {res.status_code} - {res.text}")

            else:
                print("無効な選択です。")

        except requests.exceptions.RequestException as e:
            print(f"[通信エラー] {e}")

if __name__ == "__main__":
    main()
