using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScanSnapShot.Models;

namespace ScanSnapShot.Services;

public class RemoteServerService
{
    private WebApplication? _app;
    private bool _isRunning = false;

    public bool IsRunning => _isRunning;

    // MainWindow から提供されるコールバック
    public Func<Task<string?>>? CaptureHandler { get; set; }
    public Func<AppSettings, Task<bool>>? SettingsUpdateHandler { get; set; }
    public Func<Task<bool>>? StartMonitoringHandler { get; set; }
    public Func<Task<bool>>? StopMonitoringHandler { get; set; }
    public Func<object>? StatusProvider { get; set; }
    public Func<AppSettings>? SettingsProvider { get; set; }
    public Action<string>? LogAction { get; set; }

    public async Task StartAsync()
    {
        if (_isRunning)
        {
            return;
        }

        // 独立した設定ファイル (remote_settings.json) を読み込む
        // ファイルが存在しない場合は機能無効 (起動しない)
        var remoteSettings = RemoteServerSettings.Load();
        if (remoteSettings == null || !remoteSettings.Enabled)
        {
            return;
        }

        try
        {
            var cert = GetOrCreateSelfSignedCertificate();
            var port = remoteSettings.Port > 0 ? remoteSettings.Port : 50050;
            var expectedApiKey = remoteSettings.ApiKey;

            var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
            {
                Args = Array.Empty<string>()
            });

            // Routing サービスの追加
            builder.Services.AddRouting();

            builder.WebHost.UseKestrel(options =>
            {
                options.ListenAnyIP(port, listenOptions =>
                {
                    listenOptions.UseHttps(cert);
                });
            });

            var app = builder.Build();

            // 認証ミドルウェア
            app.Use(async (context, next) =>
            {
                var path = context.Request.Path.Value ?? string.Empty;
                if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                {
                    var apiKeyHeader = context.Request.Headers["X-API-KEY"].FirstOrDefault();
                    var apiKeyQuery = context.Request.Query["apiKey"].FirstOrDefault();
                    var providedKey = !string.IsNullOrEmpty(apiKeyHeader) ? apiKeyHeader : apiKeyQuery;

                    if (!string.IsNullOrEmpty(expectedApiKey) && providedKey != expectedApiKey)
                    {
                        context.Response.StatusCode = 401;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(new
                        {
                            success = false,
                            error = "Unauthorized: 有効な API キーが指定されていません。(X-API-KEY ヘッダー または apiKey クエリが必要です)"
                        });
                        return;
                    }
                }
                await next();
            });

            app.UseRouting();

            // 1. キャプチャー実行エンドポイント
            app.MapPost("/api/capture", async (HttpContext ctx) =>
            {
                if (CaptureHandler == null)
                {
                    return Results.Json(new { success = false, error = "キャプチャーハンドラーが初期化されていません。" }, statusCode: 500);
                }

                var path = await CaptureHandler.Invoke();
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    return Results.Json(new { success = false, error = "キャプチャー撮影に失敗しました。" }, statusCode: 500);
                }

                // クエリ ?download=true または Accept ヘッダーが image/* の場合はバイナリ返却
                var downloadQuery = ctx.Request.Query["download"].FirstOrDefault();
                var acceptHeader = ctx.Request.Headers["Accept"].FirstOrDefault() ?? string.Empty;

                if (string.Equals(downloadQuery, "true", StringComparison.OrdinalIgnoreCase) ||
                    acceptHeader.Contains("image/png") ||
                    acceptHeader.Contains("image/*"))
                {
                    return Results.File(path, "image/png", Path.GetFileName(path));
                }

                var fileName = Path.GetFileName(path);
                return Results.Json(new
                {
                    success = true,
                    filePath = path,
                    fileName = fileName,
                    downloadUrl = $"/api/images/{fileName}"
                });
            });

            // 2. キャプチャーフォルダー内のファイル一覧・ファイル数取得エンドポイント
            app.MapGet("/api/images", () =>
            {
                var currentSettings = SettingsProvider?.Invoke();
                var saveDir = currentSettings?.SaveDirectory;

                if (string.IsNullOrEmpty(saveDir) || !Directory.Exists(saveDir))
                {
                    return Results.Json(new
                    {
                        success = true,
                        saveDirectory = saveDir ?? string.Empty,
                        totalCount = 0,
                        files = Array.Empty<object>()
                    });
                }

                var dirInfo = new DirectoryInfo(saveDir);
                var files = dirInfo.GetFiles("*.png")
                                   .OrderByDescending(f => f.CreationTime)
                                   .Select(f => new
                                   {
                                       fileName = f.Name,
                                       fileSizeBytes = f.Length,
                                       createdAt = f.CreationTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                                       downloadUrl = $"/api/images/{f.Name}"
                                   })
                                   .ToList();

                return Results.Json(new
                {
                    success = true,
                    saveDirectory = saveDir,
                    totalCount = files.Count,
                    files = files
                });
            });

            // 3. キャプチャー画像個別ダウンロードエンドポイント
            app.MapGet("/api/images/{fileName}", (string fileName) =>
            {
                var safeFileName = Path.GetFileName(fileName);
                var currentSettings = SettingsProvider?.Invoke();
                var saveDir = currentSettings?.SaveDirectory;

                if (string.IsNullOrEmpty(saveDir) || !Directory.Exists(saveDir))
                {
                    return Results.NotFound(new { success = false, error = "保存先フォルダーが見つかりません。" });
                }

                var fullPath = Path.Combine(saveDir, safeFileName);
                if (!File.Exists(fullPath))
                {
                    return Results.NotFound(new { success = false, error = "指定された画像ファイルが存在しません。" });
                }

                return Results.File(fullPath, "image/png", safeFileName);
            });

            // 4. 設定取得エンドポイント
            app.MapGet("/api/settings", () =>
            {
                var currentSettings = SettingsProvider?.Invoke();
                if (currentSettings == null)
                {
                    return Results.Json(new { success = false, error = "設定を取得できませんでした。" }, statusCode: 500);
                }

                return Results.Json(new
                {
                    success = true,
                    settings = new
                    {
                        currentSettings.ScanArea,
                        currentSettings.CaptureArea,
                        currentSettings.IsCaptureFullScreen,
                        currentSettings.IntervalMilliseconds,
                        currentSettings.SensitivityThresholdPercent,
                        currentSettings.CooldownMilliseconds,
                        currentSettings.SaveDirectory,
                        currentSettings.ShowThumbnail,
                        currentSettings.ThumbnailDurationSeconds
                    }
                });
            });

            // 5. 設定更新エンドポイント
            app.MapPost("/api/settings", async (HttpContext ctx) =>
            {
                if (SettingsUpdateHandler == null)
                {
                    return Results.Json(new { success = false, error = "設定更新ハンドラーが初期化されていません。" }, statusCode: 500);
                }

                using var reader = new StreamReader(ctx.Request.Body);
                var jsonBody = await reader.ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(jsonBody))
                {
                    return Results.BadRequest(new { success = false, error = "JSON ボディが空です。" });
                }

                try
                {
                    var current = SettingsProvider?.Invoke();
                    if (current == null)
                    {
                        return Results.Json(new { success = false, error = "現在の設定が読み込めません。" }, statusCode: 500);
                    }

                    var jsonDoc = JsonDocument.Parse(jsonBody);
                    var root = jsonDoc.RootElement;

                    if (root.TryGetProperty("IntervalMilliseconds", out var intervalProp) && intervalProp.TryGetInt32(out var interval))
                        current.IntervalMilliseconds = interval;

                    if (root.TryGetProperty("SensitivityThresholdPercent", out var sensProp) && sensProp.TryGetDouble(out var sens))
                        current.SensitivityThresholdPercent = sens;

                    if (root.TryGetProperty("CooldownMilliseconds", out var cdProp) && cdProp.TryGetInt32(out var cd))
                        current.CooldownMilliseconds = cd;

                    if (root.TryGetProperty("SaveDirectory", out var saveDirProp) && saveDirProp.GetString() is { } saveDir && !string.IsNullOrWhiteSpace(saveDir))
                        current.SaveDirectory = saveDir;

                    if (root.TryGetProperty("ShowThumbnail", out var thumbProp))
                        current.ShowThumbnail = thumbProp.GetBoolean();

                    if (root.TryGetProperty("ThumbnailDurationSeconds", out var durationProp) && durationProp.TryGetInt32(out var dur))
                        current.ThumbnailDurationSeconds = dur;

                    if (root.TryGetProperty("IsCaptureFullScreen", out var fullProp))
                        current.IsCaptureFullScreen = fullProp.GetBoolean();

                    if (root.TryGetProperty("ScanArea", out var scanProp))
                    {
                        var x = scanProp.GetProperty("X").GetInt32();
                        var y = scanProp.GetProperty("Y").GetInt32();
                        var w = scanProp.GetProperty("Width").GetInt32();
                        var h = scanProp.GetProperty("Height").GetInt32();
                        current.ScanArea = new AreaRect(x, y, w, h);
                    }

                    if (root.TryGetProperty("CaptureArea", out var capProp))
                    {
                        var x = capProp.GetProperty("X").GetInt32();
                        var y = capProp.GetProperty("Y").GetInt32();
                        var w = capProp.GetProperty("Width").GetInt32();
                        var h = capProp.GetProperty("Height").GetInt32();
                        current.CaptureArea = new AreaRect(x, y, w, h);
                    }

                    var updated = await SettingsUpdateHandler.Invoke(current);
                    if (!updated)
                    {
                        return Results.BadRequest(new { success = false, error = "設定値のバリデーションに失敗しました。" });
                    }

                    return Results.Json(new { success = true, message = "設定を更新しました。" });
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { success = false, error = $"設定のパースに失敗しました: {ex.Message}" });
                }
            });

            // 6. ステータス取得エンドポイント
            app.MapGet("/api/status", () =>
            {
                var status = StatusProvider?.Invoke() ?? new { isRunning = false };
                return Results.Json(new
                {
                    success = true,
                    status
                });
            });

            // 7. 監視開始
            app.MapPost("/api/monitor/start", async () =>
            {
                if (StartMonitoringHandler == null)
                {
                    return Results.Json(new { success = false, error = "監視ハンドラーが初期化されていません。" }, statusCode: 500);
                }

                var success = await StartMonitoringHandler.Invoke();
                return Results.Json(new { success, isRunning = success });
            });

            // 8. 監視停止
            app.MapPost("/api/monitor/stop", async () =>
            {
                if (StopMonitoringHandler == null)
                {
                    return Results.Json(new { success = false, error = "監視ハンドラーが初期化されていません。" }, statusCode: 500);
                }

                var success = await StopMonitoringHandler.Invoke();
                return Results.Json(new { success, isRunning = false });
            });

            _app = app;
            await _app.StartAsync();
            _isRunning = true;
            LogAction?.Invoke($"[リモートAPI] HTTPS サーバーを開始しました (ポート: {port})");
        }
        catch (Exception ex)
        {
            _isRunning = false;
            LogAction?.Invoke($"[リモートAPI エラー] サーバーの起動に失敗しました: {ex.Message}");
        }
    }

    public async Task StopAsync()
    {
        if (_app != null)
        {
            try
            {
                await _app.StopAsync();
                await _app.DisposeAsync();
            }
            catch (Exception ex)
            {
                LogAction?.Invoke($"[リモートAPI エラー] サーバー停止中に例外: {ex.Message}");
            }
            finally
            {
                _app = null;
                _isRunning = false;
            }
        }
    }

    private static X509Certificate2 GetOrCreateSelfSignedCertificate()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var certDir = Path.Combine(appData, "ScanSnapShot");
        var certPath = Path.Combine(certDir, "server.pfx");
        const string certPassword = "ScanSnapShotInternalCertPwd123!";

        if (File.Exists(certPath))
        {
            try
            {
                return X509CertificateLoader.LoadPkcs12FromFile(certPath, certPassword, X509KeyStorageFlags.Exportable);
            }
            catch
            {
                // 破損等の場合は再生成へ
            }
        }

        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=ScanSnapShotServer", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddIpAddress(IPAddress.Loopback);
        sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);

        try
        {
            var hostName = Dns.GetHostName();
            sanBuilder.AddDnsName(hostName);
            var hostEntry = Dns.GetHostEntry(hostName);
            foreach (var ip in hostEntry.AddressList)
            {
                sanBuilder.AddIpAddress(ip);
            }
        }
        catch { }

        req.CertificateExtensions.Add(sanBuilder.Build());

        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));
        var pfxBytes = cert.Export(X509ContentType.Pfx, certPassword);

        if (!Directory.Exists(certDir))
        {
            Directory.CreateDirectory(certDir);
        }
        File.WriteAllBytes(certPath, pfxBytes);

        return X509CertificateLoader.LoadPkcs12(pfxBytes, certPassword, X509KeyStorageFlags.Exportable);
    }
}
