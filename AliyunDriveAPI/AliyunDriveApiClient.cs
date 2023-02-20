using AliyunDriveAPI.Models;
using AliyunDriveAPI.Models.Converters;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;

namespace AliyunDriveAPI;

public partial class AliyunDriveApiClient
{
    private string _refreshToken;
    private string _token;
    private DateTime? _tokenExpireTime;

    private readonly HttpClient _httpClient;

    public static JsonSerializerOptions JsonSerializerOptions
        => new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = new SnakeCaseNamingPolicy(),
            Converters =
            {
                new Models.Converters.JsonStringEnumConverter(),
                new JsonNodeConverter(),
                new TimeSpanSecondConverter(),
                new NullableTimeSpanSecondConverter(),
                new DatetimeConverter(),
            }
        };

    public AliyunDriveApiClient(string refreshToken)
    {
        _refreshToken = refreshToken;
        _httpClient = new() { BaseAddress = new Uri("https://api.aliyundrive.com/") };
    }

    public async Task<RefreshTokenResponse> RefreshTokenAsync()
    {
        var obj = new JsonObject
        {
            ["refresh_token"] = _refreshToken,
            ["grant_type"] = "refresh_token"
        };
        return await SendJsonPostAsync<RefreshTokenResponse>("https://auth.aliyundrive.com/v2/account/token", obj, false);
    }

    private bool IsTokenExpire()
        => _tokenExpireTime == null || _tokenExpireTime.Value <= DateTime.Now;

    private async Task PrepareTokenAsync()
    {
        if (!IsTokenExpire()) return;
        var res = await RefreshTokenAsync();
        _token = res.AccessToken;
        _refreshToken = res.RefreshToken;
        _tokenExpireTime = res.ExpireTime;
        if (_httpClient.DefaultRequestHeaders.Contains("Authorization"))
            _httpClient.DefaultRequestHeaders.Remove("Authorization");
        _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _token);
        _httpClient.DefaultRequestHeaders.Add("x-canary", "client=web,app=adrive,version=v4.0.0");
        _httpClient.DefaultRequestHeaders.Add("x-device-id", res.DeviceId);

        var sign = await GetSign(res.DeviceId, res.UserId);
        _httpClient.DefaultRequestHeaders.Add("x-signature", sign.sign[0].ToString());
    }

    private async Task<SignResponse> GetSign(string deviceid, string userid)
    {
        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"http://ks4.kerwin.cn:19951/alisign?userid={HttpUtility.UrlEncode(userid)}&deviceid={HttpUtility.UrlEncode(deviceid)}&jwt={HttpUtility.UrlEncode(_token)}");
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        Console.WriteLine(json);

        return JsonSerializer.Deserialize<SignResponse>(json, JsonSerializerOptions);
    }
}