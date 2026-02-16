using System.Net.Http.Headers;
using System.Text.Json;

namespace PlayerRomanceSetup;

internal static class GitHubRuntimeSource
{
    private const string ApiBase = "https://api.github.com";
    private const string RawBase = "https://raw.githubusercontent.com";

    public static HttpClient CreateClient()
    {
        HttpClient client = new();
        client.Timeout = TimeSpan.FromSeconds(45);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PlayerRomanceSetup", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public static async Task<string?> GetLatestCommitShaAsync(HttpClient client, CancellationToken cancellationToken)
    {
        try
        {
            string url = $"{ApiBase}/repos/{AppConfig.Owner}/{AppConfig.Repo}/commits/{AppConfig.Branch}";
            using HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return doc.RootElement.TryGetProperty("sha", out JsonElement shaEl)
                ? shaEl.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    public static async Task<bool> DownloadRuntimeFilesAsync(
        HttpClient client,
        string targetModDir,
        bool preserveExistingConfig,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(targetModDir);

            foreach (string file in AppConfig.RequiredRuntimeFiles)
            {
                bool ok = await DownloadSingleFileAsync(client, file, targetModDir, overwrite: true, log, cancellationToken);
                if (!ok)
                {
                    log($"[Setup] Missing required file on GitHub: {file}");
                    return false;
                }
            }

            foreach (string file in AppConfig.OptionalRuntimeFiles)
            {
                bool overwrite = !(preserveExistingConfig && file.Equals("config.json", StringComparison.OrdinalIgnoreCase));
                await DownloadSingleFileAsync(client, file, targetModDir, overwrite, log, cancellationToken, required: false);
            }

            return true;
        }
        catch (Exception ex)
        {
            log($"[Setup] Download failed: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> DownloadSingleFileAsync(
        HttpClient client,
        string relativeFile,
        string targetModDir,
        bool overwrite,
        Action<string> log,
        CancellationToken cancellationToken,
        bool required = true)
    {
        try
        {
            string targetPath = Path.Combine(targetModDir, relativeFile);
            if (!overwrite && File.Exists(targetPath))
            {
                log($"[Setup] Keeping existing {relativeFile}");
                return true;
            }

            string url = $"{RawBase}/{AppConfig.Owner}/{AppConfig.Repo}/{AppConfig.Branch}/{relativeFile.Replace('\\', '/')}";
            using HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                if (required)
                {
                    log($"[Setup] Download failed: {relativeFile} ({(int)response.StatusCode})");
                }

                return !required;
            }

            byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            string? folder = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }

            await File.WriteAllBytesAsync(targetPath, bytes, cancellationToken);
            log($"[Setup] Updated {relativeFile}");
            return true;
        }
        catch
        {
            return !required;
        }
    }
}
