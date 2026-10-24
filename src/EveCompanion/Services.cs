using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EveCompanion;

public static class StateStore
{
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EveCompanionMvp",
        "state.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppState Load(out string? warning)
    {
        warning = null;
        if (!File.Exists(FilePath))
            return new AppState();

        try
        {
            return JsonSerializer.Deserialize<AppState>(File.ReadAllText(FilePath), JsonOptions) ?? new AppState();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            warning = $"Не удалось прочитать сохранённые данные: {error.Message}";
            return new AppState();
        }
    }

    public static void Save(AppState state)
    {
        var directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state, JsonOptions));
        File.Move(temporary, FilePath, true);
    }
}

public sealed record SsoAuthorization(Uri Url, string Verifier, string State);

public static class EveSso
{
    public static readonly string[] ReadScopes =
    [
        "esi-assets.read_assets.v1",
        "esi-wallet.read_character_wallet.v1",
        "esi-markets.read_character_orders.v1"
    ];

    public static SsoAuthorization CreateAuthorization(string clientId, string callbackUrl) =>
        CreateAuthorization(clientId, callbackUrl, RandomToken(), RandomToken());

    internal static SsoAuthorization CreateAuthorization(
        string clientId,
        string callbackUrl,
        string verifier,
        string state)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Укажите Client ID приложения EVE.", nameof(clientId));
        if (!Uri.TryCreate(callbackUrl, UriKind.Absolute, out _))
            throw new ArgumentException("Callback URL должен быть абсолютным URL.", nameof(callbackUrl));

        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        var challenge = Base64Url(hash);
        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["redirect_uri"] = callbackUrl,
            ["client_id"] = clientId,
            ["scope"] = string.Join(' ', ReadScopes),
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["state"] = state
        };
        var encoded = string.Join('&', query.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return new SsoAuthorization(new Uri($"https://login.eveonline.com/v2/oauth/authorize/?{encoded}"), verifier, state);
    }

    private static string RandomToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public static class EsiApi
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };

    public static async Task<MarketSnapshot> GetMarketSnapshotAsync(
        int regionId,
        int typeId,
        string userAgent,
        CancellationToken cancellationToken = default)
    {
        if (regionId <= 0 || typeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(regionId), "Region ID и Type ID должны быть положительными.");

        var url = $"https://esi.evetech.net/markets/{regionId}/orders/?datasource=tranquility&order_type=all&type_id={typeId}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        request.Headers.TryAddWithoutValidation("X-Compatibility-Date", "2026-10-24");
        using var response = await Client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        var orders = await JsonSerializer.DeserializeAsync<List<MarketOrder>>(body, cancellationToken: cancellationToken) ?? [];

        var bestBuy = orders.Where(order => order.IsBuyOrder).Select(order => (decimal?)order.Price).Max();
        var bestSell = orders.Where(order => !order.IsBuyOrder).Select(order => (decimal?)order.Price).Min();
        return new MarketSnapshot(bestBuy, bestSell, DateTimeOffset.Now);
    }

    private sealed class MarketOrder
    {
        [System.Text.Json.Serialization.JsonPropertyName("is_buy_order")]
        public bool IsBuyOrder { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("price")]
        public decimal Price { get; init; }
    }
}

public static class SelfTest
{
    public static void Run()
    {
        Require(IskMath.HourlyRate(2_000_000, TimeSpan.FromMinutes(30)) == 4_000_000, "ISK/ч");
        Require(IskMath.HourlyRate(100, TimeSpan.Zero) == 0, "нулевая длительность");
        Require(IskMath.TryParseAmount("1 250 000", out var amount) && amount == 1_250_000, "ввод ISK");

        var auth = EveSso.CreateAuthorization(
            "client-id",
            "http://localhost:53682/callback/",
            "fixed-verifier",
            "fixed-state");
        Require(auth.Url.Query.Contains("code_challenge_method=S256"), "SSO PKCE");
        Require(auth.Url.Query.Contains("state=fixed-state"), "SSO state");
    }

    private static void Require(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException($"Self-test failed: {name}");
    }
}
