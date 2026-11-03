using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FeishuPixelTasks;

internal sealed class CredentialVault(string directory)
{
    private string FileName => Path.Combine(directory, "credentials.secret");
    public bool Exists => File.Exists(FileName);
    private static readonly string[] Allowed = ["DOUBAO_OFFICE_CLI_TOKEN", "DOUBAO_OFFICE_USER_ACCESS_TOKEN"];
    public void Import(string path)
    {
        if (new FileInfo(path).Length > 65536) throw new InvalidDataException("凭据文件过大。");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var values = new Dictionary<string, string>();
        foreach (var name in Allowed)
            if (doc.RootElement.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                values[name] = item.GetString()!;
        if (values.Count == 0) throw new InvalidDataException("文件中没有受支持的 CLI 凭据字段。");
        var data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(values));
        var sealedBytes = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(FileName + ".tmp", sealedBytes);
        File.Move(FileName + ".tmp", FileName, true);
        CryptographicOperations.ZeroMemory(data);
    }
    public Dictionary<string, string> Read()
    {
        if (!Exists) return [];
        try
        {
            var bytes = ProtectedData.Unprotect(File.ReadAllBytes(FileName), null, DataProtectionScope.CurrentUser);
            try { return JsonSerializer.Deserialize<Dictionary<string, string>>(bytes) ?? []; }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        catch (CryptographicException) { throw new InvalidOperationException("凭据无法解密，请在当前 Windows 用户下重新导入。"); }
    }
    public void Clear() { if (Exists) File.Delete(FileName); }
}
