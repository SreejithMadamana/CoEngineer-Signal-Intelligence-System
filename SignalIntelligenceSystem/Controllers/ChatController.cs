using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

public class ChatMessageModel
{
    public string UserMessage { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private static ConcurrentDictionary<string, ChatSessionState> _sessions = new();
    private static ConcurrentDictionary<string, string> _results = new();
    private readonly SignalOrchestratorService _signalOrchestratorService;
    private readonly OllamaService _ollamaService;
    private readonly ILLMService _llmService;
    public ChatController(
        SignalOrchestratorService signalOrchestratorService,
        OllamaService ollamaService, ILLMService llMservice)
    {
        _signalOrchestratorService = signalOrchestratorService;
        _ollamaService = ollamaService;
        _llmService = llMservice;
    }

    [HttpPost("message")]
    public async Task<IActionResult> PostMessage([FromQuery] string sessionId, [FromBody] ChatMessageModel model)
    {
        var userMessage = model.UserMessage;
        var session = _sessions.GetOrAdd(sessionId, _ => new ChatSessionState());
        session.Messages.Add(new LLMMessage { Role = "user", Content = userMessage });

        // Step 0: Detect greeting or non-task message
        var greetings = new[] { "hi", "hello", "hey", "greetings", "good morning", "good afternoon", "good evening" };
        if (session.Messages.Count == 1 && greetings.Any(g => userMessage?.ToLower().StartsWith(g) == true))
        {
            return Ok(new
            {
                response = "Hello! I'm here to support you in generating signal lists for industrial devices. Please tell me what device and protocol you'd like to work with.",
                isComplete = false
            });
        }

        // Step 1: Use LLM to parse user input for all fields
        var validDeviceTypes = _signalOrchestratorService._signalTemplateService.GetDeviceTemplateKeys();
        validDeviceTypes.Remove("RequiredAttributes");
        if (!string.IsNullOrEmpty(session.DeviceType))
        {
            var deviceTemplate = _signalOrchestratorService._signalTemplateService.GetDeviceTemplate(session.DeviceType);        
        }

        var prompt = $@"Extract the following information from the user's message and previous selections:
                                    - Device Type (choose from: {string.Join(", ", validDeviceTypes)}): {session.DeviceType ?? ""}
                                    - Protocol (e.g., Modbus, Ethernet/IP): {session.Protocol ?? ""}
                                    - Device Count (number): {session.DeviceCount?.ToString() ?? ""}
                                       The user may reply with just the missing information. Extract and update only the missing field.
                                Examples:
                                - ""I need 3 motors with Modbus"" → DeviceType: Motor, DeviceCount: 3, Protocol: Modbus
                                - ""2 Motors with Modbus protocol"" → DeviceType: Motor, DeviceCount: 2, Protocol: Modbus

                                User message: ""{userMessage}""

                                Respond in JSON:
                                {{
                                    ""DeviceType"": """",
                                    ""Protocol"": """",
                                    ""DeviceCount"": 0,
                                     ""FeedbackSignals"": []
                                }}

                            Output ONLY a raw JSON object, no extra text, no explanation, and NO code block markers or Markdown formatting.";

        var llmResponse = await _llmService.GetLLMResponseAsync(prompt);
        LLMParsedRequest parsed = null;
        try
        {
            parsed = JsonConvert.DeserializeObject<LLMParsedRequest>(llmResponse);
        }
        catch
        {
            return Ok(new { response = "Sorry, I couldn't understand your request. Please rephrase or provide more details.", isComplete = false });
        }

        // Step 2: Update only missing fields in session
        if (!string.IsNullOrEmpty(parsed.DeviceType))
            session.DeviceType = parsed.DeviceType.ToUpper();
        if (!string.IsNullOrEmpty(parsed.Protocol))
            session.Protocol = parsed.Protocol.ToUpper().Replace(" ","").Replace("-","");
        if (parsed.DeviceCount > 0)
            session.DeviceCount = parsed.DeviceCount;      
        if (parsed.FeedbackSignals != null && parsed.FeedbackSignals.Count > 0)
            session.FeedbackSignals = parsed.FeedbackSignals;
        // Step 3: Validate session and prompt for missing info
        if (string.IsNullOrEmpty(session.DeviceType))
            return Ok(new { response = $"What is the device type? (choose from: {string.Join(", ", validDeviceTypes)})", isComplete = false });
        var template = _signalOrchestratorService._signalTemplateService.GetDeviceTemplate(session.DeviceType);
        if (string.IsNullOrEmpty(session.Protocol))
            return Ok(new { response = "Which protocol do you want to use? Available: " + string.Join(", ", template.ProtocolDefaults.Keys), isComplete = false });
        var protocolExists = template?.ProtocolDefaults?.Keys
     .Any(k => string.Equals(k, session.Protocol, StringComparison.OrdinalIgnoreCase)) ?? false;
        var deviceTypeExists = template != null;
        if (!deviceTypeExists)
            return Ok(new { response = $"What is the device type? (choose from: {string.Join(", ", validDeviceTypes)})", isComplete = false });
        if (!protocolExists)
            return Ok(new { response = $"Which protocol do you want to use? Available: {string.Join(", ", template.ProtocolDefaults.Keys)}", isComplete = false });
        if (session.DeviceCount == null || session.DeviceCount <= 0)
            return Ok(new { response = "How many devices do you want to generate the signal list for?", isComplete = false });
        // If user did not provide feedback signals, use all from template
        if ((session.FeedbackSignals == null || session.FeedbackSignals.Count == 0) && template?.FeedbackSignals != null)
        {
            session.FeedbackSignals = template.FeedbackSignals.Keys.ToList();
        }
        // Step 4: If all info is present, generate signal list
        session.SignalRequest = new SignalRequest
        {
            DeviceType = session.DeviceType,
            DeviceCount = session.DeviceCount ?? 1,
            Protocol = session.Protocol,
            FeedbackSignals = session.FeedbackSignals ?? new List<string>()
        };
        session.IsComplete = true;
        _ = Task.Run(async () =>
        {
            var (success, error, response) = await _signalOrchestratorService.GenerateArtifactsAsync(session.SignalRequest);
            if (success && response != null)
            {
                _results[sessionId] = JsonConvert.SerializeObject(new
                {
                    json = response.JsonContent,
                    csv = response.CsvContent,
                    xlsx = response.XlsxContent != null ? Convert.ToBase64String(response.XlsxContent) : null
                });
            }
            else
            {
                _results[sessionId] = JsonConvert.SerializeObject(new
                {
                    json = $"Error: {error}",
                    csv = ""
                });
            }
        });
        _sessions[sessionId] = session;
        return Ok(new { response = "Generating signal list, please wait...", isComplete = true });
    }

    [HttpGet("result")]
    public IActionResult GetResult([FromQuery] string sessionId)
    {
        if (_results.TryGetValue(sessionId, out var result))
        {
            return Ok(new { ready = true, result });
        }
        return Ok(new { ready = false, result = "" });
    }

    [HttpPost("SendSignalListToApi")]
    public async Task<IActionResult> SendSignalListToApi([FromBody] JsonElement body)
    {
        // Raw JSON text exactly as received
        var rawJson = body.GetRawText();
        if (string.IsNullOrWhiteSpace(rawJson))
            return BadRequest("Empty JSON body.");
        var apiUrl = "https://localhost:44309/v1/objects";
        using var httpClient = new HttpClient();
        var content = new StringContent(rawJson, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(apiUrl, content);
        var downstreamPayload = await response.Content.ReadAsStringAsync();
        if (response.IsSuccessStatusCode)
            return Ok("Signal list sent successfully.");
        return StatusCode((int)response.StatusCode, downstreamPayload);
    }
}

