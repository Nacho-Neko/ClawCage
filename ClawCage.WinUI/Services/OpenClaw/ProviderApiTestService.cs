using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Media.Protection.PlayReady;

namespace ClawCage.WinUI.Services.OpenClaw
{
    internal static class ProviderApiTestService
    {
        private static readonly HttpClient HttpClient = new();


        internal static async Task<(bool Success, string Message)> TestCompatibleAsync(string apiKey, string requestUri, string model)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return (false, "测试失败：API Key 不能为空。");

            HttpClient httpClient = new HttpClient();
            var payload = new
            {
                model,
                max_tokens = 1,
                temperature = 0,
                messages = new[]
                {
                    new { role = "user", content = "ping" }
                }
            };


            var jsonPayload = JsonSerializer.Serialize(payload);
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("curl/7.68.0");
            httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
            httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            httpClient.DefaultRequestHeaders.Connection.Add("Keep-Alive");

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync(requestUri, content);

            var responseBody = await response.Content.ReadAsStringAsync();

            return response.IsSuccessStatusCode
                ? (true, $"{(int)response.StatusCode}")
                : (false, $"请求失败: {(int)response.StatusCode} {response.StatusCode}");
        }
    }
}
