using System.Security.Cryptography;
using System.Text;

namespace Oohra.Server;

/// <summary>
/// Tajný klíč hráče: hra si ho při prvním spuštění vytvoří a uloží na počítači. Server si u přezdívky pamatuje
/// jen otisk klíče (SHA-256), ne klíč samotný. Kdo zná jen přezdívku, za cizí postavu hrát nemůže.
/// </summary>
public static class PlayerKeys
{
    public static string Hash(string playerKey)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(playerKey)));
    }

    /// <summary>
    /// Smí hráč s tímto klíčem hrát za postavu? Postava bez otisku (nová, nebo ji správce odemkl příkazem unlock)
    /// patří tomu, kdo přijde první.
    /// </summary>
    public static bool Matches(string? storedHash, string playerKey)
    {
        return storedHash is null || storedHash == Hash(playerKey);
    }
}
