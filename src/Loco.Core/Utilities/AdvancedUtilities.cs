using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Loco.Core.Utilities;

/// <summary>
/// 拡張ファイル操作機能
/// </summary>
public class AdvancedFileOperations
{
    /// <summary>
    /// 大きなファイルをチャンク単位で処理
    /// </summary>
    public static async Task ProcessLargeFileAsync(string filePath, int chunkSize, Func<byte[], Task> processor)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        var buffer = new byte[chunkSize];

        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            var chunk = new byte[bytesRead];
            Buffer.BlockCopy(buffer, 0, chunk, 0, bytesRead);
            await processor(chunk);
        }
    }

    /// <summary>
    /// ファイルの重複を検出
    /// </summary>
    public static async Task<IEnumerable<FileDuplicateGroup>> FindDuplicatesAsync(string directoryPath, SearchOption searchOption = SearchOption.AllDirectories)
    {
        var files = Directory.GetFiles(directoryPath, "*.*", searchOption);
        var fileGroups = new Dictionary<string, List<string>>();

        foreach (var file in files)
        {
            try
            {
                var hash = await CalculateFileHashAsync(file);
                if (!fileGroups.ContainsKey(hash))
                {
                    fileGroups[hash] = new List<string>();
                }
                fileGroups[hash].Add(file);
            }
            catch
            {
                // ファイルにアクセスできない場合はスキップ
            }
        }

        return fileGroups
            .Where(g => g.Value.Count > 1)
            .Select(g => new FileDuplicateGroup { Hash = g.Key, Files = g.Value })
            .ToList();
    }

    /// <summary>
    /// ファイルを安全に移動（トランザクション方式）
    /// </summary>
    public static async Task SafeMoveFileAsync(string sourcePath, string destinationPath)
    {
        var tempPath = Path.Combine(Path.GetDirectoryName(destinationPath) ?? "",
            Guid.NewGuid().ToString() + Path.GetExtension(destinationPath));

        try
        {
            // 一時ファイルにコピー
            using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
            using (var tempStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            {
                await sourceStream.CopyToAsync(tempStream);
            }

            // 検証
            if (!await VerifyFilesIdenticalAsync(sourcePath, tempPath))
            {
                throw new IOException("File copy verification failed");
            }

            // 元のファイルを削除
            File.Delete(sourcePath);

            // 一時ファイルを最終位置に移動
            File.Move(tempPath, destinationPath);
        }
        catch
        {
            // エラーが発生した場合、一時ファイルをクリーンアップ
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
            throw;
        }
    }

    /// <summary>
    /// ファイルの類似性を分析
    /// </summary>
    public static async Task<double> CalculateFileSimilarityAsync(string filePath1, string filePath2)
    {
        const int sampleSize = 1024;

        using var stream1 = new FileStream(filePath1, FileMode.Open, FileAccess.Read);
        using var stream2 = new FileStream(filePath2, FileMode.Open, FileAccess.Read);

        var buffer1 = new byte[sampleSize];
        var buffer2 = new byte[sampleSize];

        var bytesRead1 = await stream1.ReadAsync(buffer1, 0, sampleSize);
        var bytesRead2 = await stream2.ReadAsync(buffer2, 0, sampleSize);

        if (bytesRead1 != bytesRead2)
            return 0;

        int differences = 0;
        for (int i = 0; i < Math.Min(bytesRead1, bytesRead2); i++)
        {
            if (buffer1[i] != buffer2[i])
                differences++;
        }

        return 1.0 - (double)differences / Math.Min(bytesRead1, bytesRead2);
    }

    /// <summary>
    /// ファイルのメタデータを取得
    /// </summary>
    public static FileMetadata GetFileMetadata(string filePath)
    {
        var info = new FileInfo(filePath);
        return new FileMetadata
        {
            Path = filePath,
            Name = info.Name,
            Extension = info.Extension,
            Size = info.Length,
            Created = info.CreationTimeUtc,
            Modified = info.LastWriteTimeUtc,
            Accessed = info.LastAccessTimeUtc,
            Attributes = info.Attributes,
            IsReadOnly = info.IsReadOnly,
            Hash = CalculateFileHash(filePath)
        };
    }

    /// <summary>
    /// ファイルのハッシュを計算
    /// </summary>
    private static string CalculateFileHash(string filePath)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// ファイルのハッシュを非同期で計算
    /// </summary>
    private static async Task<string> CalculateFileHashAsync(string filePath)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// ファイルが同一かどうかを検証
    /// </summary>
    private static async Task<bool> VerifyFilesIdenticalAsync(string filePath1, string filePath2)
    {
        var hash1 = await CalculateFileHashAsync(filePath1);
        var hash2 = await CalculateFileHashAsync(filePath2);
        return hash1 == hash2;
    }

    /// <summary>
    /// ファイル重複グループ
    /// </summary>
    public class FileDuplicateGroup
    {
        public string Hash { get; set; } = "";
        public List<string> Files { get; set; } = new();
    }

    /// <summary>
    /// ファイルメタデータ
    /// </summary>
    public class FileMetadata
    {
        public string Path { get; set; } = "";
        public string Name { get; set; } = "";
        public string Extension { get; set; } = "";
        public long Size { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public DateTime Accessed { get; set; }
        public FileAttributes Attributes { get; set; }
        public bool IsReadOnly { get; set; }
        public string Hash { get; set; } = "";
    }
}

/// <summary>
/// ネットワークツール機能
/// </summary>
public class NetworkTools
{
    private static readonly HttpClient _httpClient = new();

    static NetworkTools()
    {
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// ホストの到達可能性をテスト
    /// </summary>
    public static async Task<PingResult> PingHostAsync(string host, int timeoutMs = 5000)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, timeoutMs);

            return new PingResult
            {
                Host = host,
                Success = reply.Status == IPStatus.Success,
                RoundTripTime = reply.RoundtripTime,
                Ttl = reply.Options?.Ttl ?? 0,
                Status = reply.Status
            };
        }
        catch (Exception ex)
        {
            return new PingResult
            {
                Host = host,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// ポートスキャンを実行
    /// </summary>
    public static async Task<IEnumerable<PortScanResult>> ScanPortsAsync(string host, int startPort, int endPort)
    {
        var results = new List<PortScanResult>();
        var tasks = new List<Task<PortScanResult>>();

        for (int port = startPort; port <= endPort; port++)
        {
            tasks.Add(ScanPortAsync(host, port));
        }

        var scanResults = await Task.WhenAll(tasks);
        return scanResults.Where(r => r.IsOpen).OrderBy(r => r.Port);
    }

    private static async Task<PortScanResult> ScanPortAsync(string host, int port)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            var timeoutTask = Task.Delay(5000);

            var completedTask = await Task.WhenAny(connectTask, timeoutTask);

            if (completedTask == connectTask && client.Connected)
            {
                return new PortScanResult { Host = host, Port = port, IsOpen = true };
            }
            else
            {
                return new PortScanResult { Host = host, Port = port, IsOpen = false };
            }
        }
        catch
        {
            return new PortScanResult { Host = host, Port = port, IsOpen = false };
        }
    }

    /// <summary>
    /// HTTPリクエストを送信
    /// </summary>
    public static async Task<HttpResponseResult> SendHttpRequestAsync(string url, HttpMethod method = null, string content = null)
    {
        method ??= HttpMethod.Get;

        try
        {
            var request = new HttpRequestMessage(method, url);

            if (!string.IsNullOrEmpty(content) && (method == HttpMethod.Post || method == HttpMethod.Put))
            {
                request.Content = new StringContent(content, Encoding.UTF8, "application/json");
            }

            var startTime = DateTime.Now;
            var response = await _httpClient.SendAsync(request);
            var responseTime = DateTime.Now - startTime;

            var responseContent = await response.Content.ReadAsStringAsync();

            return new HttpResponseResult
            {
                Url = url,
                Method = method.Method,
                StatusCode = (int)response.StatusCode,
                StatusDescription = response.StatusCode.ToString(),
                ResponseTime = responseTime,
                ContentLength = responseContent.Length,
                Content = responseContent,
                Headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
                Success = response.IsSuccessStatusCode
            };
        }
        catch (Exception ex)
        {
            return new HttpResponseResult
            {
                Url = url,
                Method = method.Method,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// DNS解決を実行
    /// </summary>
    public static async Task<DnsResolutionResult> ResolveDnsAsync(string hostname)
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(hostname);
            return new DnsResolutionResult
            {
                Hostname = hostname,
                Success = true,
                Addresses = addresses.Select(a => a.ToString()).ToList()
            };
        }
        catch (Exception ex)
        {
            return new DnsResolutionResult
            {
                Hostname = hostname,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// ネットワーク統計を取得
    /// </summary>
    public static NetworkStatistics GetNetworkStatistics()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
            .ToList();

        var stats = new NetworkStatistics();

        foreach (var ni in interfaces)
        {
            var interfaceStats = ni.GetIPv4Statistics();
            stats.TotalBytesSent += interfaceStats.BytesSent;
            stats.TotalBytesReceived += interfaceStats.BytesReceived;
            stats.TotalPacketsSent += interfaceStats.UnicastPacketsSent;
            stats.TotalPacketsReceived += interfaceStats.UnicastPacketsReceived;
        }

        return stats;
    }

    /// <summary>
    /// Ping結果
    /// </summary>
    public class PingResult
    {
        public string Host { get; set; } = "";
        public bool Success { get; set; }
        public long RoundTripTime { get; set; }
        public int Ttl { get; set; }
        public IPStatus Status { get; set; }
        public string ErrorMessage { get; set; } = "";
    }

    /// <summary>
    /// ポートスキャン結果
    /// </summary>
    public class PortScanResult
    {
        public string Host { get; set; } = "";
        public int Port { get; set; }
        public bool IsOpen { get; set; }
    }

    /// <summary>
    /// HTTPレスポンス結果
    /// </summary>
    public class HttpResponseResult
    {
        public string Url { get; set; } = "";
        public string Method { get; set; } = "";
        public int StatusCode { get; set; }
        public string StatusDescription { get; set; } = "";
        public TimeSpan ResponseTime { get; set; }
        public int ContentLength { get; set; }
        public string Content { get; set; } = "";
        public Dictionary<string, string> Headers { get; set; } = new();
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = "";
    }

    /// <summary>
    /// DNS解決結果
    /// </summary>
    public class DnsResolutionResult
    {
        public string Hostname { get; set; } = "";
        public bool Success { get; set; }
        public List<string> Addresses { get; set; } = new();
        public string ErrorMessage { get; set; } = "";
    }

    /// <summary>
    /// ネットワーク統計
    /// </summary>
    public class NetworkStatistics
    {
        public long TotalBytesSent { get; set; }
        public long TotalBytesReceived { get; set; }
        public long TotalPacketsSent { get; set; }
        public long TotalPacketsReceived { get; set; }
    }
}

/// <summary>
/// データ変換機能
/// </summary>
public class DataConversion
{
    /// <summary>
    /// JSONをXMLに変換
    /// </summary>
    public static string JsonToXml(string json)
    {
        try
        {
            using var jsonDoc = System.Text.Json.JsonDocument.Parse(json);
            return JsonElementToXml(jsonDoc.RootElement, "root");
        }
        catch
        {
            throw new ArgumentException("Invalid JSON format");
        }
    }

    /// <summary>
    /// XMLをJSONに変換
    /// </summary>
    public static string XmlToJson(string xml)
    {
        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(xml);
            return System.Text.Json.JsonSerializer.Serialize(XmlToDictionary(doc.Root), new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            throw new ArgumentException("Invalid XML format");
        }
    }

    /// <summary>
    /// CSVをJSONに変換
    /// </summary>
    public static string CsvToJson(string csv, bool hasHeaders = true)
    {
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return "[]";

        var headers = hasHeaders ? lines[0].Split(',').Select(h => h.Trim()).ToArray() : null;
        var dataLines = hasHeaders ? lines.Skip(1) : lines;

        var records = new List<Dictionary<string, string>>();

        foreach (var line in dataLines)
        {
            var values = ParseCsvLine(line);
            var record = new Dictionary<string, string>();

            for (int i = 0; i < values.Length; i++)
            {
                var key = headers != null && i < headers.Length ? headers[i] : $"Column{i + 1}";
                record[key] = values[i];
            }

            records.Add(record);
        }

        return JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// JSONをCSVに変換
    /// </summary>
    public static string JsonToCsv(string json)
    {
        try
        {
            var data = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);
            if (data == null || data.Count == 0) return "";

            var headers = data.First().Keys.ToArray();
            var csv = new StringBuilder();
            csv.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

            foreach (var record in data)
            {
                var values = headers.Select(h => record.TryGetValue(h, out var value) ? value?.ToString() ?? "" : "");
                csv.AppendLine(string.Join(",", values.Select(v => $"\"{v.Replace("\"", "\"\"")}\"")));
            }

            return csv.ToString();
        }
        catch
        {
            throw new ArgumentException("Invalid JSON array format");
        }
    }

    /// <summary>
    /// テキストエンコーディングを変換
    /// </summary>
    public static string ConvertEncoding(string text, Encoding fromEncoding, Encoding toEncoding)
    {
        var bytes = fromEncoding.GetBytes(text);
        return toEncoding.GetString(bytes);
    }

    /// <summary>
    /// テキストをBase64にエンコード
    /// </summary>
    public static string EncodeBase64(string text, Encoding encoding = null)
    {
        encoding ??= Encoding.UTF8;
        var bytes = encoding.GetBytes(text);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Base64をテキストにデコード
    /// </summary>
    public static string DecodeBase64(string base64Text, Encoding encoding = null)
    {
        encoding ??= Encoding.UTF8;
        var bytes = Convert.FromBase64String(base64Text);
        return encoding.GetString(bytes);
    }

    /// <summary>
    /// テキストをURLエンコード/デコード
    /// </summary>
    public static string UrlEncode(string text) => Uri.EscapeDataString(text);
    public static string UrlDecode(string text) => Uri.UnescapeDataString(text);

    /// <summary>
    /// テキストをHTMLエンコード/デコード
    /// </summary>
    public static string HtmlEncode(string text) => System.Net.WebUtility.HtmlEncode(text);
    public static string HtmlDecode(string text) => System.Net.WebUtility.HtmlDecode(text);

    private static string JsonElementToXml(System.Text.Json.JsonElement element, string elementName)
    {
        var xml = new System.Xml.Linq.XElement(elementName);

        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    xml.Add(JsonElementToXml(property.Value, property.Name));
                }
                break;
            case System.Text.Json.JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    xml.Add(JsonElementToXml(item, "item"));
                }
                break;
            default:
                xml.Value = element.ToString();
                break;
        }

        return xml.ToString();
    }

    private static Dictionary<string, object> XmlToDictionary(System.Xml.Linq.XElement element)
    {
        var result = new Dictionary<string, object>();

        if (element.HasElements)
        {
            var elements = element.Elements();
            if (elements.All(e => e.Name.LocalName == "item"))
            {
                // 配列の場合
                return new Dictionary<string, object>
                {
                    [element.Name.LocalName] = elements.Select(XmlToDictionary).ToList()
                };
            }
            else
            {
                // オブジェクトの場合
                foreach (var child in elements)
                {
                    result[child.Name.LocalName] = XmlToDictionary(child);
                }
            }
        }
        else
        {
            result[element.Name.LocalName] = element.Value;
        }

        return result;
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++; // 次の文字をスキップ
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        result.Add(current.ToString());
        return result.ToArray();
    }
}

/// <summary>
/// 拡張バックアップ機能
/// </summary>
public class AdvancedBackup
{
    /// <summary>
    /// 増分バックアップを作成
    /// </summary>
    public static async Task CreateIncrementalBackupAsync(string sourcePath, string backupPath, string manifestPath)
    {
        var manifest = LoadBackupManifest(manifestPath);
        var changes = FindChangedFiles(sourcePath, manifest);

        if (changes.Any())
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var incrementalBackupPath = Path.Combine(backupPath, $"incremental_{timestamp}.zip");

            await CreateZipArchiveAsync(changes, incrementalBackupPath);

            // マニフェストを更新
            foreach (var file in changes)
            {
                var relativePath = Path.GetRelativePath(sourcePath, file);
                manifest[relativePath] = new FileInfo(file).LastWriteTimeUtc;
            }

            SaveBackupManifest(manifestPath, manifest);
        }
    }

    /// <summary>
    /// 完全バックアップを作成
    /// </summary>
    public static async Task CreateFullBackupAsync(string sourcePath, string backupPath, string manifestPath)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fullBackupPath = Path.Combine(backupPath, $"full_{timestamp}.zip");

        var allFiles = Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories);
        await CreateZipArchiveAsync(allFiles, fullBackupPath);

        // マニフェストを作成
        var manifest = new Dictionary<string, DateTime>();
        foreach (var file in allFiles)
        {
            var relativePath = Path.GetRelativePath(sourcePath, file);
            manifest[relativePath] = new FileInfo(file).LastWriteTimeUtc;
        }

        SaveBackupManifest(manifestPath, manifest);
    }

    /// <summary>
    /// バックアップから復元
    /// </summary>
    public static async Task RestoreFromBackupAsync(string backupPath, string restorePath, bool overwrite = false)
    {
        if (!File.Exists(backupPath))
            throw new FileNotFoundException("Backup file not found", backupPath);

        if (!Directory.Exists(restorePath))
            Directory.CreateDirectory(restorePath);

        using var archive = ZipFile.OpenRead(backupPath);
        foreach (var entry in archive.Entries)
        {
            var destinationPath = Path.Combine(restorePath, entry.FullName);

            if (!overwrite && File.Exists(destinationPath))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? "");

            using var entryStream = entry.Open();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
            await entryStream.CopyToAsync(fileStream);
        }
    }

    /// <summary>
    /// バックアップの整合性を検証
    /// </summary>
    public static async Task<BackupVerificationResult> VerifyBackupIntegrityAsync(string backupPath)
    {
        var result = new BackupVerificationResult();

        try
        {
            using var archive = ZipFile.OpenRead(backupPath);

            foreach (var entry in archive.Entries)
            {
                result.TotalFiles++;

                try
                {
                    using var entryStream = entry.Open();
                    using var memoryStream = new MemoryStream();
                    await entryStream.CopyToAsync(memoryStream);

                    if (memoryStream.Length != entry.Length)
                    {
                        result.CorruptedFiles++;
                        result.Errors.Add($"Size mismatch for {entry.FullName}");
                    }
                }
                catch (Exception ex)
                {
                    result.CorruptedFiles++;
                    result.Errors.Add($"Error reading {entry.FullName}: {ex.Message}");
                }
            }

            result.IsValid = result.CorruptedFiles == 0;
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Failed to open backup: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// 古いバックアップをクリーンアップ
    /// </summary>
    public static void CleanupOldBackups(string backupDirectory, TimeSpan retentionPeriod, int maxBackups = 10)
    {
        var backupFiles = Directory.GetFiles(backupDirectory, "*.zip")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .ToList();

        // 保持期間を超えたファイルを削除
        var cutoffDate = DateTime.Now - retentionPeriod;
        var filesToDelete = backupFiles.Where(f => f.CreationTime < cutoffDate).ToList();

        foreach (var file in filesToDelete)
        {
            try
            {
                file.Delete();
            }
            catch
            {
                // 削除に失敗した場合はスキップ
            }
        }

        // 最大数を超えたファイルを削除
        var remainingFiles = Directory.GetFiles(backupDirectory, "*.zip")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .Skip(maxBackups)
            .ToList();

        foreach (var file in remainingFiles)
        {
            try
            {
                file.Delete();
            }
            catch
            {
                // 削除に失敗した場合はスキップ
            }
        }
    }

    private static Dictionary<string, DateTime> LoadBackupManifest(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            return new Dictionary<string, DateTime>();

        try
        {
            var json = File.ReadAllText(manifestPath);
            return JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json) ?? new Dictionary<string, DateTime>();
        }
        catch
        {
            return new Dictionary<string, DateTime>();
        }
    }

    private static void SaveBackupManifest(string manifestPath, Dictionary<string, DateTime> manifest)
    {
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(manifestPath, json);
    }

    private static IEnumerable<string> FindChangedFiles(string sourcePath, Dictionary<string, DateTime> manifest)
    {
        var changedFiles = new List<string>();

        foreach (var file in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourcePath, file);
            var lastWriteTime = new FileInfo(file).LastWriteTimeUtc;

            if (!manifest.TryGetValue(relativePath, out var lastBackupTime) || lastBackupTime < lastWriteTime)
            {
                changedFiles.Add(file);
            }
        }

        return changedFiles;
    }

    private static async Task CreateZipArchiveAsync(IEnumerable<string> files, string archivePath)
    {
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);

        foreach (var file in files)
        {
            if (File.Exists(file))
            {
                var entryName = Path.GetRelativePath(Path.GetDirectoryName(archivePath) ?? "", file);
                archive.CreateEntryFromFile(file, entryName);
            }
        }
    }

    /// <summary>
    /// バックアップ検証結果
    /// </summary>
    public class BackupVerificationResult
    {
        public bool IsValid { get; set; }
        public int TotalFiles { get; set; }
        public int CorruptedFiles { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
