# ScanSnapShot

画面の特定エリア（**ScanArea**）の変化をリアルタイムに監視し、変化を検知した瞬間に指定の撮影エリア（**CaptureArea**）を自動キャプチャーして保存する .NET 10 WPF デスクトップアプリケーションです。

---

## 🌟 主な特徴

- **2つの独立したエリア設定**:
  - **ScanArea（監視エリア）**: 画面の変化・トリガーを監視する範囲。
  - **CaptureArea（撮影エリア）**: 実際にキャプチャーして画像として保存する範囲。
- **直感的なドラッグ選択オーバーレイ**:
  - マルチモニター対応の全画面半透明オーバーレイで、マウスドラッグのみで直感的に範囲を指定可能。
- **軽量・高精度な差分検知**:
  - 高速ピクセル走査（32bpp ARGB）による変化率（%）判定。わずかなチラつきやノイズによる誤検知を防止する感度閾値設定。
- **設定の永続化**:
  - 指定した範囲（X, Y, W, H）、チェック間隔、保存先フォルダーなどは自動的に `%APPDATA%\ScanSnapShot\settings.json` に保存され、次回起動時に自動復元。
- **タスクトレイ常駐対応**:
  - 監視開始時およびウィンドウ最小化時に自動でタスクトレイ（通知領域）に格納。
  - トレイアイコンの右クリックメニューから監視開始/終了やアプリ終了が可能。
- **NLog による柔軟なロギング**:
  - 実行ファイル直下の `logs/` フォルダーに日別ログ（`yyyy_MM_dd_ScanSnapShot.log`）を自動保存。
  - 30日を経過した古いログファイルを自動的にクリーンアップ。
  - `NLog.config` の編集により、アプリ起動中であっても出力フォーマット等を動的に変更可能。

---

## 🛠️ 技術スタック

- **フレームワーク**: .NET 10.0 (Windows Desktop)
- **UIテクノロジー**: WPF (Windows Presentation Foundation) / XAML
- **開発言語**: C# 14
- **ロギング**: NLog 6.x
- **ソリューション形式**: Visual Studio 2026 対応 (`ScanSnapShot.slnx`)

---

## 📂 プロジェクト構成

```text
C:\Users\zm7wb\source\repos\ScanSnapShot\
├── ScanSnapShot.slnx                 # Visual Studio 2026 ソリューション
├── README.md                          # 本書（概要・仕様）
├── USER_MANUAL.md                     # 詳細操作説明書
└── ScanSnapShot/                      # プロジェクト本体
    ├── ScanSnapShot.csproj            # プロジェクト定義
    ├── app.ico / app.png              # アプリケーションアイコン
    ├── NLog.config                    # NLog ログ設定ファイル
    ├── App.xaml / App.xaml.cs         # アプリケーションエントリ
    ├── MainWindow.xaml / .cs          # メイン操作画面
    ├── Models/
    │   ├── AreaRect.cs                # 座標・矩形モデル
    │   └── AppSettings.cs             # 設定データモデル
    ├── Services/
    │   ├── ScreenCaptureService.cs    # 画面キャプチャー＆差分計算エンジン
    │   ├── WatcherService.cs          # バックグラウンド定期監視ループ
    │   ├── SettingsService.cs         # JSON設定保存・読み込み
    │   ├── TrayIconService.cs         # タスクトレイ・コンテキストメニュー
    │   └── LogService.cs              # NLog 初期化
    └── Views/
        └── AreaSelectionWindow.xaml   # 範囲選択オーバーレイウィンドウ
```

---

## 🚀 ビルドと実行方法

### 必須要件
- Windows 10 / 11 (64-bit)
- .NET 10.0 SDK または Visual Studio 2026 (WPFワークロード導入済み)

### コマンドラインからの実行
```bash
# ビルド
dotnet build ScanSnapShot.slnx

# アプリケーション起動
dotnet run --project ScanSnapShot/ScanSnapShot.csproj
```

操作手順の詳細については [USER_MANUAL.md](USER_MANUAL.md) をご覧ください。
