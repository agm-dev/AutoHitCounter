//

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AutoHitCounter.Models.Twitch;

namespace AutoHitCounter.Utilities;

/// <summary>
/// Persists the Twitch tokens outside settings.txt, encrypted with DPAPI so only the current
/// Windows account can read them back. settings.txt is plain text and is the wrong place for a
/// credential that can change someone's channel.
/// </summary>
public static class TwitchTokenStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AutoHitCounter.Twitch.v1");

    private static string StorePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AutoHitCounter",
        "twitch.dat");

    public static TwitchTokens Load()
    {
        try
        {
            if (!File.Exists(StorePath)) return null;

            var encrypted = File.ReadAllBytes(StorePath);
            var plain = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<TwitchTokens>(Encoding.UTF8.GetString(plain));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Could not read the stored Twitch tokens");
            return null;
        }
    }

    public static void Save(TwitchTokens tokens)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);

            var plain = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(tokens));
            var encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(StorePath, encrypted);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Could not store the Twitch tokens");
        }
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(StorePath)) File.Delete(StorePath);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Could not delete the stored Twitch tokens");
        }
    }
}
