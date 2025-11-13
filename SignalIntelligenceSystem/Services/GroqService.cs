using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class GroqService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public GroqService(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public async Task<string> GetLLMResponseAsync(string prompt)
    {
        var requestBody = new
        {
            model = "llama-3.3-70b-versatile", // or another Groq-supported model
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("chat/completions", content);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseString);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
    }
}