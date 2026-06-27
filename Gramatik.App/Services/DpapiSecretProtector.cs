using System.Security.Cryptography;
using System.Text;
using Gramatik.App.Models;

namespace Gramatik.App.Services;

public sealed class DpapiSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Gramatik.OpenRouter.ApiKey.v1");

    public string Protect(string value)
    {
        var plainBytes = Encoding.UTF8.GetBytes(value);
        var protectedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public string Unprotect(string protectedValue)
    {
        var protectedBytes = Convert.FromBase64String(protectedValue);
        var plainBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
