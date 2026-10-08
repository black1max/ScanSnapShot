"""
別PC操作用スクリプト (remote_client_download.py)
ネットワーク上の別PC（ScanSnapShot稼働PC）を操作し、
キャプチャー画像を直接ダウンロードして手元のPCに保存します。

使い方:
  python remote_client_download.py <相手PCのIPアドレス> [APIキー] [ポート番号]
例:
  python remote_client_download.py 192.168.1.50
"""

import os
import sys
import requests
from datetime import datetime
from config import create_session, DEFAULT_PORT, DEFAULT_API_KEY

def main():
    # 引数から接続先IPを取得
    if len(sys.argv) > 1:
        remote_ip = sys.argv[1]
    else:
        remote_ip = input("接続先のIPアドレスを入力してください (例: 192.168.1.50): ").strip()
        if not remote_ip:
            print("IPアドレスが入力されなかったため終了します。")
            return

    api_key = sys.argv[2] if len(sys.argv) > 2 else DEFAULT_API_KEY
    port = int(sys.argv[3]) if len(sys.argv) > 3 else DEFAULT_PORT

    base_url = f"https://{remote_ip}:{port}"
    session = create_session(api_key=api_key)

    print("==================================================")
    print(" ScanSnapShot リモート操作 & 画像ダウンロードクライアント")
    print(f" 接続先: {base_url}")
    print("==================================================")

    # 1. 接続確認
    try:
        print("\n[1] 相手PCに接続テスト中...")
        res = session.get(f"{base_url}/api/status", timeout=5)
        if res.status_code == 200:
            status = res.json().get("status", {})
            print(f"  -> 接続成功！ 監視稼働状態: {'監視中' if status.get('isRunning') else '待機中'}")
        elif res.status_code == 401:
            print("  -> [認証エラー] APIキーが一致しません。")
            return
        else:
            print(f"  -> エラー: {res.status_code} - {res.text}")
            return
    except requests.exceptions.RequestException as e:
        print(f"  -> [接続失敗] 相手PCに接続できませんでした: {e}")
        print("  -> 相手PCで Windows ファイアウォール (ポート 50050) が許可されているか確認してください。")
        return

    # 2. 相手PCでキャプチャーを実行し、画像バイナリを直接手元にダウンロード
    print("\n[2] リモートキャプチャーを実行し、手元のPCに画像をダウンロードします...")
    
    # ?download=true を指定することで画像ファイルそのものが返却される
    capture_url = f"{base_url}/api/capture?download=true"
    res = session.post(capture_url, headers={"Accept": "image/png"})
    
    if res.status_code == 200 and "image/png" in res.headers.get("Content-Type", ""):
        # 手元のカレントディレクトリに保存
        save_dir = os.path.join(os.path.dirname(__file__), "downloaded_images")
        os.makedirs(save_dir, exist_ok=True)
        
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        local_filename = f"remote_capture_{timestamp}.png"
        local_filepath = os.path.join(save_dir, local_filename)

        with open(local_filepath, "wb") as f:
            f.write(res.content)

        print(f"  -> ダウンロード完了！")
        print(f"  -> 手元に保存したファイル: {local_filepath}")
        print(f"  -> ファイルサイズ: {len(res.content):,} bytes")
    else:
        print(f"  -> キャプチャーに失敗しました: {res.status_code} - {res.text}")

    # 3. 相手PCの保存先フォルダー内のファイル一覧・枚数確認
    print("\n[3] 相手PCのキャプチャーフォルダー状況を取得中...")
    res = session.get(f"{base_url}/api/images")
    if res.status_code == 200:
        data = res.json()
        print(f"  -> 相手PCの保存フォルダー: {data.get('saveDirectory')}")
        print(f"  -> 相手PCの総画像数: {data.get('totalCount')} 枚")
    else:
        print(f"  -> 一覧取得失敗: {res.status_code}")

if __name__ == "__main__":
    main()
