using OllamaSharp;
using OllamaSharp.Models;
using System.Text;
using Newtonsoft.Json;
public class LLMMessage
{
    public string Role { get; set; } // "user" or "assistant"
    public string Content { get; set; }
}
public class OllamaService : ILLMService
{
    private readonly OllamaApiClient _client;
    private const string SystemPrompt = "You are an assistant that ONLY helps users generate signal lists for industrial devices. If the user asks anything unrelated to signal list generation, reply: 'I can only assist with generating signal lists for industrial devices. Please provide details related to signal list generation.'";

    public OllamaService()
    {
        _client = new OllamaApiClient("http://localhost:11434");
    }

    public async Task<string> GetLLMResponseAsync(string prompt)
    {
        var request = new GenerateRequest
        {
            Model = "llama3:8b", // or any model  pulled
            Prompt = prompt,
            Options = new RequestOptions
            {
                Temperature = (float?)0.2
            },
            Stream = true
        };

        var sb = new StringBuilder();
        await foreach (var chunk in _client.GenerateAsync(request))
        {
            if (chunk?.Response != null)
            {
                sb.Append(chunk.Response);
            }
        }
        return sb.ToString();
    }

    // Multi-turn chat support with system prompt
    public async Task<string> GetLLMChatResponseAsync(List<LLMMessage> messages)
    {
        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine($"system: {SystemPrompt}");
        foreach (var msg in messages)
        {
            promptBuilder.AppendLine($"{msg.Role}: {msg.Content}");
        }
        var prompt = promptBuilder.ToString();
        var request = new GenerateRequest
        {
            Model = "llama3.2",
            Prompt = prompt,
            Options = new RequestOptions
            {
                Temperature = (float?)0.2
            },
            Stream = true
        };
        var sb = new StringBuilder();
        await foreach (var chunk in _client.GenerateAsync(request))
        {
            if (chunk?.Response != null)
            {
                sb.Append(chunk.Response);
            }
        }
        return sb.ToString();
    }

    // New: Parse LLM response directly to LLMParsedRequest
    public async Task<LLMParsedRequest> GetParsedSignalRequestAsync(string userMessage)
    {
        var prompt = $@"Extract the following information from the user's message:
- Device Type (e.g., Motor, PLC, VFD)
- Protocol (e.g., Modbus, Ethernet/IP)
- Device Count (number)
- Control Method (e.g., VFD, Direct Start)
- Feedback Signals (list, e.g., Temperature, Current, Vibration)

User message: ""{userMessage}""

Respond in JSON:
{{
  ""DeviceType"": """",
  ""Protocol"": """",
  ""DeviceCount"": 0,
  ""ControlMethod"": """",
  ""FeedbackSignals"": []
}}";

        var llmResponse = await GetLLMResponseAsync(prompt);
        try
        {
            return JsonConvert.DeserializeObject<LLMParsedRequest>(llmResponse);
        }
        catch
        {
            return null;
        }
    }
}