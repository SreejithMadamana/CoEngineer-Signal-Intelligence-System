using Newtonsoft.Json;
public class ResponseParserService : IResponseParserService
{
    // Parse LLM output directly to SignalItem list (expects array of dictionaries)
    public List<SignalItem> Parse(string llmOutput)
    {
        var items = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(llmOutput)
            ?? new List<Dictionary<string, object>>();
        return items.Select(dict => new SignalItem { Attributes = dict }).ToList();
    }

    // Parse LLM output and map to SignalDefinition using known keys
    public List<SignalDefinition> ParseWithMetadata(string llmOutput, string protocol)
    {
        var items = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(llmOutput)
            ?? new List<Dictionary<string, object>>();

        return items.Select(dict => new SignalDefinition
        {
            SIG_NAME = dict.TryGetValue("SIG_NAME", out var name) ? name?.ToString() : null,
            SIG_DESC = dict.TryGetValue("SIG_DESC", out var desc) ? desc?.ToString() : null,
            SIG_TYPE = dict.TryGetValue("SIG_TYPE", out var type) ? type?.ToString() : null
        }).ToList();
    }
}