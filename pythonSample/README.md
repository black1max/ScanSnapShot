# ScanSnapShot Python クライアントサンプル集 (`pythonSample`)

ScanSnapShot のリモート操作機能（HTTPS / REST API）を Python から呼び出すためのサンプルスクリプト群です。
同一PC上のローカル操作、同一LAN内の別PCからのリモート操作、画像の一括/個別ダウンロード、設定更新などに対応しています。

---

## 1. 事前準備 (ScanSnapShot 側)

ScanSnapShot のリモート機能は、**設定ファイルが存在する場合のみ有効化**されます。

1. ScanSnapShot が動く PC の設定フォルダーを開きます:  
   `%APPDATA%\ScanSnapShot\`  
   *(例: `C:\Users\<ユーザー名>\AppData\Roaming\ScanSnapShot\`)*
2. 上記フォルダーに **`remote_settings.json`** という名前のファイルを新規作成し、以下を保存します:
   ```json
   {
     "Enabled": true,
     "Port": 50050,
     "ApiKey": "scansnapshot_secret_key"
   }
   ```
3. ScanSnapShot を起動（または再起動）します。起動ログに `[リモートAPI] HTTPS サーバーを開始しました (ポート: 50050)` と表示されれば準備完了です。
4. **別PCからアクセスする場合のみ**:  
   ScanSnapShot 稼働PCの Windows ファイアウォールでポート `50050` (TCP) の受信を許可してください。

---

## 2. Python 側のセットアップ

Python 3.8 以上がインストールされた環境で、必要なライブラリをインストールします:

```bash
pip install requests
```

---

## 3. スクリプト一覧と使い方

| ファイル名 | 対象環境 | 主な機能・用途 |
| :--- | :--- | :--- |
| **`config.py`** | 共通 | 共通設定（ホスト、ポート、APIキー、SSL検証スキップ設定など） |
| **`local_client_simple.py`** | ローカルPC | 同一PC上でキャプチャー撮影、枚数・画像一覧の取得を行うシンプルスクリプト |
| **`remote_client_download.py`** | 別PC | ネットワーク経由で撮影を実行し、手元のPCへPNG画像を直接ダウンロード保存 |
| **`remote_settings_manager.py`** | ローカル / 別PC | 撮影モード（全画面 / 指定範囲）や監視間隔等の設定をリモートから確認・変更 |
| **`interactive_console.py`** | ローカル / 別PC | メニュー番号を選んで全ての機能を直感的にテストできる対話式コンソール |

---

## 4. 各スクリプトの実行例

### ① ローカルPC上で動作確認 (`local_client_simple.py`)
同一PCで ScanSnapShot を起動した状態で実行します:
```bash
python local_client_simple.py
```

### ② 別PCから撮影＆手元にダウンロード (`remote_client_download.py`)
ScanSnapShot が動いている PC の IP アドレスを指定して実行します:
```bash
python remote_client_download.py 192.168.1.50
```
※撮影された PNG 画像は、自動的に `pythonSample/downloaded_images/` フォルダーに保存されます。

### ③ リモートから設定を変更 (`remote_settings_manager.py`)
相手PCの撮影エリアや感度をリモートで切り替えます:
```bash
python remote_settings_manager.py 192.168.1.50
```

### ④ メニューから手軽に対話操作 (`interactive_console.py`)
```bash
python interactive_console.py 192.168.1.50
```
メニューが表示され、状態確認、キャプチャー、枚数・ファイル一覧表示、任意画像のダウンロード、監視開始・停止などを番号入力で操作できます。
