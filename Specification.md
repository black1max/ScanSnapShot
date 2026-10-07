# ScanSnapShot 詳細仕様書 (System Specification)

## 1. ドキュメント概要
本書は、デスクトップ画面の特定領域監視および変化検知時における自動キャプチャーを行う Windows アプリケーション **ScanSnapShot** の詳細仕様書である。

---

## 2. システム概要・実行環境

| 項目 | 仕様 |
| :--- | :--- |
| **アプリケーション名** | ScanSnapShot |
| **ターゲットプラットフォーム** | Windows 10 / 11 (x64) |
| **ランタイムフレームワーク** | .NET 10.0 (WindowsDesktop / WPF) |
| **開発言語** | C# 14.0 |
| **ソリューション形式** | Visual Studio 2026 ソリューション形式 (`ScanSnapShot.slnx`) |
| **プロジェクト形式** | SDK スタイル (`ScanSnapShot.csproj`) |
| **主要パッケージ** | `NLog` (6.2.1), `System.Drawing.Common` (10.0.12) |
| **コンパイル設定** | `AllowUnsafeBlocks: true` (画像高速比較用), `UseWPF: true`, `UseWindowsForms: true` (タスクトレイ・フォルダー選択用) |

---

## 3. システムアーキテクチャ・クラス設計

```text
[ MainWindow (UI) ]
    │
    ├─► [ AreaSelectionWindow ] (画面領域選択オーバーレイ)
    │
    ├─► [ SettingsService ] ───► [ %APPDATA%\ScanSnapShot\settings.json ]
    │
    ├─► [ WatcherService ] (バックグラウンド定期監視)
    │       │
    │       ├─► [ ScreenCaptureService ] (GDI+ キャプチャー & ピクセルポインタ差分計算)
    │       │
    │       └─► 保存先フォルダー (PNG画像保存)
    │
    ├─► [ TrayIconService ] (NotifyIcon 常駐 / コンテキストメニュー制御)
    │
    └─► [ LogService ] ───► [ NLog.config ] ───► [ logs/yyyy_MM_dd_ScanSnapShot.log ]
```

### 主要クラス一覧

| クラス名 | 名前空間 | 役割 |
| :--- | :--- | :--- |
| `MainWindow` | `ScanSnapShot` | メイン画面。パラメータ入力、監視制御、ログ表示、最小化制御を統括。 |
| `AreaSelectionWindow` | `ScanSnapShot.Views` | 画面ドラッグによるエリア指定オーバーレイウィンドウ。 |
| `AreaRect` | `ScanSnapShot.Models` | 座標 (`X`, `Y`) およびサイズ (`Width`, `Height`) を保持するモデル。 |
| `AppSettings` | `ScanSnapShot.Models` | 設定ファイル（JSON）とバインドするデータモデル。 |
| `SettingsService` | `ScanSnapShot.Services` | 設定の JSON シリアライズ / デシリアライズ永続化サービス。 |
| `ScreenCaptureService` | `ScanSnapShot.Services` | GDI+ 画面キャプチャーおよびポインタ走査による画像差分率計算。 |
| `WatcherService` | `ScanSnapShot.Services` | `Task.Run` および `CancellationToken` による定期画面監視ループ制御。 |
| `TrayIconService` | `ScanSnapShot.Services` | `System.Windows.Forms.NotifyIcon` をラップしたタスクトレイ制御。 |
| `LogService` | `ScanSnapShot.Services` | NLog の初期化およびログ出力中継。 |

---

## 4. 機能仕様

### 4.1 エリア設定機能
1. **2つの独立したエリア管理**:
   * **ScanArea（監視エリア）**: 変化を検知するためのトリガーエリア。
   * **CaptureArea（キャプチャーエリア）**: 変化検知時に実際に撮影・保存するエリア。
   * **同期機能**: 「ScanAreaと同じにする」ボタンにより、ScanArea の座標・サイズを CaptureArea に即座に複製可能。
2. **範囲指定オーバーレイ (`AreaSelectionWindow`)**:
   * `SystemParameters.VirtualScreenLeft` / `Top` / `Width` / `Height` を使用し、マルチモニター環境全域をカバー。
   * 背景透過度 `0.2` の黒色マスクを表示。
   * マウス左ボタンドラッグにより矩形を描画（青枠＋半透明背景）。
   * 画面左上にリアルタイム座標（X, Y, W, H）をツールチップ形式で表示。
   * **確定条件**: `Enter` キー押下、または**選択エリア内部のダブルクリック**。
   * **キャンセル条件**: `Esc` キー押下。

### 4.2 監視・変化検知エンジン (`WatcherService`, `ScreenCaptureService`)
1. **監視ループ**:
   * 監視開始時、別スレッド（`Task.Run`）にて非同期ループを開始。
   * 指定された `IntervalMilliseconds`（間隔）ごとにループ。
2. **差分検知アルゴリズム (`CalculateDifferencePercentage`)**:
   * 初回ループで ScanArea の基準 Bitmap をメモリに保持。
   * 2回目以降、最新 Bitmap と基準 Bitmap を比較。
   * `Bitmap.LockBits` による 32bpp ARGB のポインタ走査を実施。
   * 各ピクセルにおける R, G, B の絶対差分値が `25`（許容誤差閾値）を超えるピクセルを「変化ピクセル」としてカウント。
   * $\text{変化率(\%)} = \frac{\text{変化ピクセル数}}{\text{総ピクセル数}} \times 100$
3. **トリガーとクールダウン**:
   * 変化率が `SensitivityThresholdPercent`（変化しきい値 %）以上の場合、直ちに `CaptureArea` の Bitmap を取得して保存。
   * 基準 Bitmap を最新取得 Bitmap に更新。
   * `CooldownMilliseconds`（クールダウン時間）が指定されている場合、連続撮影防止のため待機（Delay）。

### 4.3 パラメータ入力仕様とバリデーション
メイン画面の入力値には以下の制限を設け、範囲外の値が入力された状態で「監視開始」または「テストキャプチャー」を実行した場合は警告ダイアログを表示して処理を中止する。

| パラメータ | 項目名 | 型 | 許容範囲 | デフォルト値 | 単位 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| チェック間隔 | `IntervalMilliseconds` | 整数 (int) | `50` 〜 `300,000` | `1000` | ms |
| 変化しきい値 | `SensitivityThresholdPercent` | 実数 (double) | `0.1` 〜 `100.0` | `3.0` | % |
| クールダウン | `CooldownMilliseconds` | 整数 (int) | `0` 〜 `300,000` | `1500` | ms |
| 保存先フォルダー | `SaveDirectory` | 文字列 (string) | 有効なパス | `Pictures\ScanSnapShot` | - |

### 4.4 画像保存仕様
* **保存形式**: PNG 形式 (`.png`)
* **ファイル名命名規則**:
  ```text
  Snap_{yyyy}_{MM}_{dd}_{HH}_{mm}_{ss}_{fff}.png
  ```
  *(例: `Snap_2026_10_07_22_47_39_263.png`)*
* **フォルダー自動生成**: 保存先フォルダーが存在しない場合は実行時に自動作成。

### 4.5 タスクトレイ（NotifyIcon）常駐仕様
1. **トレイ格納タイミング**:
   * メイン画面で「監視開始」ボタンを押下したとき。
   * メインウィンドウの「最小化」ボタン（`_`）を押下したとき。
   * 監視中にウィンドウの「×」ボタンを押下したとき（監視誤終了防止）。
2. **トレイアイコンの操作**:
   * **左クリック / ダブルクリック**: メインウィンドウを表示・最前面化。
   * **右クリックコンテキストメニュー**:
     * 停止時: 「メイン画面を表示」「監視・キャプチャーを開始」「アプリケーション終了」
     * 監視中: 「メイン画面を表示」「監視・キャプチャーを終了」「アプリケーション終了」
3. **バルーン通知**:
   * キャプチャー保存成功時、Windows 通知領域にトースト通知（バルーンチップ）を表示。

### 4.6 ログ仕様 (`NLog`)
1. **ファイル構成**:
   * **出力先**: `[exe配置ディレクトリ]\logs\`
   * **ファイル名**: `${date:format=yyyy_MM_dd}_ScanSnapShot.log`
     *(例: `2026_10_07_ScanSnapShot.log`)*
2. **フォーマット**:
   ```text
   yyyy-MM-dd HH:mm:ss.fff [LEVEL] メッセージ
   ```
3. **ローテーション・自動削除仕様**:
   * `archiveEvery="Day"` による日別自動ローテーション。
   * `maxArchiveDays="30"` および `maxArchiveFiles="30"` により、**30日を超えた古いログファイルを NLog が自動消去**。
4. **外部設定ファイル (`NLog.config`)**:
   * `autoReload="true"` を指定。アプリ稼働中であっても `NLog.config` を直接編集して保存することで、再起動なしにログフォーマット・出力レベルが動的に更新される。

### 4.7 設定永続化仕様
* **保存先**: `%APPDATA%\ScanSnapShot\settings.json`
* **保存契機**:
  * 範囲選択（ScanArea / CaptureArea）決定時
  * 「ScanAreaと同じにする」ボタン押下時
  * 「フォルダー参照」での保存先選択時
  * 「監視開始」「テストキャプチャー」実行時
  * アプリケーション終了（`OnWindowClosing`）時
* **読み込み契機**: アプリケーション起動時（`MainWindow` コンストラクタ）

---

## 5. UI / UX 仕様

1. **ウィンドウ外観**:
   * タイトル: `ScanSnapShot - 画面監視＆自動キャプチャー`
   * アイコン: 専用デザインの `app.ico` を適用。
   * 初期サイズ: 幅 `760px`, 高さ `580px`
   * 最小サイズ: 幅 `640px`, 高さ `480px` (`MinWidth`, `MinHeight`)
   * サイズ変更: `ResizeMode="CanResize"` によりユーザーによるリサイズを許可。
2. **レスポンシブ配置**:
   * ボタン配置部に `WrapPanel` を採用し、ウィンドウ幅が狭い場合でも「ScanAreaと同じにする」等のボタンが見切れないよう自動折り返し。
3. **ツールチップ (Tips)**:
   * パラメータ入力欄・ラベルにマウスホバーした際、推奨値および設定の意味をポップアップ表示。
4. **ログ・ステータス表示**:
   * 画面下部にスクロール対応のテキストログエリア（等幅フォント）を配置。
   * ステータスバーに現在の監視状態およびリアルタイムな差分検知率（%）を表示。
