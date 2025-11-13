using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SignalIntelligenceSystem.Interfaces;
using SignalIntelligenceSystem.Services;
using System.Text;

public class SignalOrchestratorService
{
    private readonly IInputValidatorService _validator;  
    private readonly OllamaService _llm;
    private readonly ILLMService _ILLMservice;
    private readonly IFileGeneratorService _fileGen;
    private readonly SignalModelService _signalModelService;
    internal readonly SignalTemplateService _signalTemplateService;
    private readonly SignalGenerationSession _session;
    public SignalOrchestratorService(
        IInputValidatorService validator,     
        OllamaService llm, ILLMService ILLMservice,
        IFileGeneratorService fileGen,
        SignalModelService signalModelService,
        SignalTemplateService signalTemplateService,
        SignalGenerationSession session
    )
    {
        _ILLMservice = ILLMservice;
        _validator = validator;        
        _llm = llm;
        _fileGen = fileGen;
        _signalModelService = signalModelService;
        _signalTemplateService = signalTemplateService;
        _session = session;
    }

    public async Task<(bool Success, string Error, SignalResponse? Response)> GenerateArtifactsAsync(SignalRequest request)
    {
        if (!_validator.Validate(request, out var error))
            return (false, error, null);
        var deviceTemplate = _signalTemplateService.GetDeviceTemplate(request.DeviceType);
        var signalNames = new List<string>();
        signalNames.AddRange(deviceTemplate.Default.BaseSignals.Select(s => s.SIG_NAME));
        if (deviceTemplate.ControlMethods != null)
            foreach (var methodSignals in deviceTemplate.ControlMethods.Values)
                signalNames.AddRange(methodSignals.Select(s => s.SIG_NAME));
        if (deviceTemplate.FeedbackSignals != null)
            signalNames.AddRange(deviceTemplate.FeedbackSignals.Values.Select(s => s.SIG_NAME));
        var allSignalsList = _signalTemplateService.GetAllSignals(request.DeviceType.ToUpper());

        var requiredAttributes = _signalTemplateService.GetRequiredAttributes(request.Protocol);
        var requiredAttributesList = string.Join(", ", requiredAttributes);
        var requiredSignalNames = string.Join(", ", signalNames);
        // Construct the signal list with all attributes
        var allSignals = new List<Dictionary<string, object>>();
        for (int i = 1; i <= request.DeviceCount; i++)
        {
            var instancePrefix = $"{request.DeviceType}_{i}";
            foreach (var sigName in signalNames.Distinct())
            {
                var signalDict = new Dictionary<string, object>
                {
                    ["Signal Name"] = $"{instancePrefix}_{sigName}",
                    ["Reference Tag"] = instancePrefix
                };
                foreach (var attr in requiredAttributes)
                {
                    if (!signalDict.ContainsKey(attr))
                        signalDict[attr] = ""; // or a suitable default value
                }
                allSignals.Add(signalDict);
            }
        }
        var config = JObject.Parse(File.ReadAllText("Config/signal_templates.json"));
     
        string prompt = GenerateLLMPrompt(config, request.Protocol, request.DeviceType, request.DeviceCount, requiredSignalNames,requiredAttributes);


        _session.LastPrompt = prompt;

        var llmOutputRaw = await _ILLMservice.GetLLMResponseAsync(prompt);
        
        int startIdx = llmOutputRaw.IndexOf('[');
        int endIdx = llmOutputRaw.IndexOf(']', startIdx);     
        List<Dictionary<string, object>> enrichedSignals;
        try
        {
            if (request.Protocol.ToLower() == "opcua")
            {
                enrichedSignals = JsonConvert.DeserializeObject<List<Dictionary<string, JToken>>>(llmOutputRaw)
    ?.Select(dict => dict.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value))
    .ToList()
    ?? new List<Dictionary<string, object>>();
            }
            else
            {
                enrichedSignals = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(llmOutputRaw)
                ?? new List<Dictionary<string, object>>();
            }

            SanitizeAttributeKeys(enrichedSignals);
            foreach (var signal in enrichedSignals)
            {
                foreach (var attr in requiredAttributes)
                {
                    if (!signal.ContainsKey(attr))
                        signal[attr] = ""; // or a suitable default value
                }
            }
            // Replace existing OPCUA post-processing block with this:
            if (request.Protocol.Equals("OPCUA", StringComparison.OrdinalIgnoreCase))
            {
                var allowedConfigNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "DataType","LowEuRange","HighEuRange","UnitId","Fraction","SSPValue","UseSSPValue","ReferenceTag"
                };

                // Helper: derive a readable description from available fields or name
                string DeriveDescription(IDictionary<string, object> original, string sanitizedName)
                {
                    // Check common keys that might carry description
                    string[] candidateKeys =
                    {
                        "SIG_DESC","Signal Description","SIGNAL DESCRIPTION","Description","DESC"
                    };
                    foreach (var k in candidateKeys)
                    {
                        if (original.TryGetValue(k, out var v) && v is string s && !string.IsNullOrWhiteSpace(s))
                            return s.Trim();
                    }

                    // If original had a nested object with 'description'
                    if (original.TryGetValue("metaDetails", out var md) && md is IDictionary<string, object> mdict &&
                        mdict.TryGetValue("description", out var dval) && dval is string dstr && !string.IsNullOrWhiteSpace(dstr))
                        return dstr.Trim();

                    // Fallback: build from sanitized name (split camel case)
                    var parts = System.Text.RegularExpressions.Regex
                        .Replace(sanitizedName, "([a-z])([A-Z])", "$1 $2")
                        .Split(new[] { '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var baseDesc = string.Join(" ", parts);
                    return $"{baseDesc} signal";
                }

                enrichedSignals = enrichedSignals.Select(signal =>
                {
                    var newSignal = new Dictionary<string, object>();

                    // object
                    var objJ = signal.TryGetValue("object", out var oVal) && oVal is JObject oj ? oj : null;
                    var rawId = objJ?["id"]?.ToString();
                    var objectId = NormalizeGuid(rawId);
                    var rawName = objJ?["name"]?.ToString()
                                 ?? signal.GetValueOrDefault("OBJECT NAME", "")?.ToString()
                                 ?? signal.GetValueOrDefault("Signal Name", "")?.ToString()
                                 ?? signal.GetValueOrDefault("SIGNAL NAME", "")?.ToString()
                                 ?? "";
                    var objectName = SanitizeOpcUaName(rawName);
                    var objectPath = objJ?["path"] is JArray pArr ? pArr.ToObject<List<object>>() : new List<object>();

                    newSignal["object"] = new Dictionary<string, object>
                    {
                        ["id"] = objectId,
                        ["name"] = rawName,
                        ["path"] = objectPath
                    };
                    string signalType = "AnalogInSignalType";
                    if(rawName.Contains("TEMP"))
                    {
                        signalType = "AnalogInSignalType";
                    }
                    else if(rawName.Contains("START_CMD"))
                    {
                        signalType = "DIntInSignalType";

                    }
                    else if (rawName.Contains("RUN_FB"))
                    {
                        signalType = "DIntOutSignalType";

                    }
                    newSignal["extends"] = new[]
                        {
                        new Dictionary<string, object>
                        {
                            ["id"] = "00000000-0000-0000-0000-000000000000",
                            ["path"] = new List<object>(),
                            ["name"] = signalType,
                            ["library"] = new Dictionary<string, object>
                            {
                                ["name"] = "SignalCommonLibrary",
                                ["majorVersion"] = 1
                            }
                        }
                    };

                    newSignal["structureElements"] = new List<object>();

                    // Extract existing OPCUA config settings if present
                    var configSettingsSource = signal.TryGetValue("models", out var mVal) && mVal is JArray ma
                        ? ma.FirstOrDefault()?["modelBody"]?["configSettings"] as JArray
                        : null;

                    var existingConfig = configSettingsSource?
                        .Select(c => new { name = c?["name"]?.ToString(), value = c?["value"]?.ToString() })
                        .Where(c => c != null && c.name != null && allowedConfigNames.Contains(c.name))
                        .Select(c => new Dictionary<string, object> { ["name"] = c!.name!, ["value"] = c.value ?? "" })
                        .ToList()
                        ?? new List<Dictionary<string, object>>();

                    void Ensure(string n, string v)
                    {
                        if (!existingConfig.Any(cs => string.Equals(cs["name"]?.ToString(), n, StringComparison.OrdinalIgnoreCase)))
                            existingConfig.Add(new Dictionary<string, object> { ["name"] = n, ["value"] = v });
                    }

                    Ensure("DataType", "Float");
                    Ensure("LowEuRange", "0.0");
                    Ensure("HighEuRange", "100.0");
                    Ensure("UnitId", "0");
                    Ensure("Fraction", "1");
                    Ensure("SSPValue", "");
                    Ensure("UseSSPValue", "False");

                    // ReferenceTag from earlier generated simple signals
                    var refTag = signal.GetValueOrDefault("Reference Tag")?.ToString()
                                 ?? signal.GetValueOrDefault("ReferenceTag")?.ToString()
                                 ?? "";
                    if (!string.IsNullOrWhiteSpace(refTag))
                        Ensure("ReferenceTag", refTag);

                    // Temperature engineering unit rule
                    if (rawName.IndexOf("temp", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var unitSetting = existingConfig
                            .FirstOrDefault(cs => string.Equals(cs["name"]?.ToString(), "UnitId", StringComparison.OrdinalIgnoreCase));
                        if (unitSetting != null)
                            unitSetting["value"] = "4408652";
                        else
                            existingConfig.Add(new Dictionary<string, object> { ["name"] = "UnitId", ["value"] = "4408652" });
                    }

                    // Build models
                    var modelDict = new Dictionary<string, object>
                    {
                        ["modelName"] = new Dictionary<string, object>
                        {
                            ["modelBaseName"] = "Signal",
                            ["modelSuffix"] = ""
                        },
                        ["modelBody"] = new Dictionary<string, object>
                        {
                            ["propertySettings"] = new List<object>(),
                            ["actualChildren"] = new List<object>(),
                            ["actualReferences"] = new List<object>(),
                            ["configSettings"] = existingConfig
                        },
                        ["isDraft"] = false,
                        ["statusIndication"] = 0
                    };

                    newSignal["models"] = new[] { modelDict };
                    newSignal["modelTypes"] = new List<object>();

                    // NEW: metaDetails description
                    var description = DeriveDescription(signal, objectName);
                    newSignal["metaDetails"] = new Dictionary<string, object>
                    {
                        ["description"] = description
                    };

                    newSignal["isDraft"] = false;
                    newSignal["statusIndication"] = 0;

                    return newSignal;
                }).ToList();
            }
        }
        catch (Exception ex)
        {
            return (false, "LLM output could not be parsed as expected signal/tag list.", null);
        }

        var files = _fileGen.GenerateFiles(enrichedSignals, _signalTemplateService);
        return (true, string.Empty, files);
    }
    private static void SanitizeAttributeKeys(List<Dictionary<string, object>> signals)
    {
        foreach (var dict in signals)
        {
            var underscored = dict.Keys.Where(k => k.Contains('_')).ToList();
            foreach (var oldKey in underscored)
            {
                var newKey = oldKey.Replace('_', ' ');
                if (dict.ContainsKey(newKey)) continue; // avoid overwrite if already present
                dict[newKey] = dict[oldKey];
                dict.Remove(oldKey);
            }
            var  ioa = dict.Keys.Where(K => K.Equals("IOA")).FirstOrDefault();
            if(ioa!=null)
            {
                var newKey = "IOA (INFORMATION OBJECT ADDRESS)";
                dict[newKey] = dict[ioa];
                dict.Remove(ioa);
            }
        }
    }
    private static string SanitizeOpcUaName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Var1";
        var parts = name.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var p in parts)
        {
            if (p.Length == 0) continue;
            sb.Append(char.ToUpperInvariant(p[0]));
            if (p.Length > 1) sb.Append(p.Substring(1).ToLowerInvariant());
        }
        var cleaned = sb.ToString();
        if (!char.IsLetter(cleaned[0])) cleaned = "X" + cleaned;
        return cleaned.Length > 64 ? cleaned.Substring(0, 64) : cleaned;
    }

    private static string NormalizeGuid(string? candidate, bool allowAllZero = false)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return Guid.NewGuid().ToString();
        if (!Guid.TryParse(candidate, out var g)) return Guid.NewGuid().ToString();
        if (g == Guid.Empty && !allowAllZero) return Guid.NewGuid().ToString();
        var flat = candidate.Replace("-", "");
        if (flat.Distinct().Count() == 1 && !allowAllZero) return Guid.NewGuid().ToString();
        return candidate;
    }

    public static string GenerateLLMPrompt(
     JObject config,
     string protocol,
     string deviceType,
     int deviceCount,
     string requiredSignalNames,
     List<string> requiredAttributes)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are a JSON generator. Output ONLY a valid JSON array. No explanations, no markdown.");
        sb.AppendLine($"Device Type: {deviceType}; Device Count: {deviceCount}; Protocol: {protocol}");
        sb.AppendLine($"Base Signals (each must be generated for EVERY device instance): {requiredSignalNames}");
        sb.AppendLine();
        sb.AppendLine("DEVICE INSTANCE NAMING:");
        sb.AppendLine($"Instances: " + string.Join(", ", Enumerable.Range(1, deviceCount).Select(i => $"{deviceType.ToUpper()}{i}")));
        sb.AppendLine($"Per signal object: \"Signal Name\" MUST be <DEVICE_INSTANCE>_<BASE_SIGNAL> (e.g. {deviceType.ToUpper()}1_RUN_FB).");
        sb.AppendLine("\"Reference Tag\" MUST be ONLY the device instance name (e.g. MOTOR1) — no signal suffixes.");
        sb.AppendLine();

        if (protocol.Equals("OPCUA", StringComparison.OrdinalIgnoreCase))
        {
            // Keep existing OPCUA block (unchanged for brevity)
            sb.AppendLine("OPCUA STRUCTURE (STRICT):");
            sb.AppendLine(@"
{
  ""object"": { ""id"": ""<GUID>"", ""name"": ""<Signal Name>"", ""path"": [] },
  ""extends"": [
    {
      ""id"": ""<GUID_BASE>"",
      ""path"": [],
      ""name"": ""AnalogInSignalType"",
      ""library"": { ""name"": ""SignalCommonLibrary"", ""majorVersion"": 1 }
    }
  ],
  ""structureElements"": [],
  ""models"": [
    {
      ""modelName"": { ""modelBaseName"": ""Signal"", ""modelSuffix"": """" },
      ""modelBody"": {
        ""propertySettings"": [],
        ""actualChildren"": [],
        ""actualReferences"": [],
        ""configSettings"": [
          { ""name"": ""DataType"", ""value"": ""Float"" },
          { ""name"": ""LowEuRange"", ""value"": ""0.0"" },
          { ""name"": ""HighEuRange"", ""value"": ""100.0"" },
          { ""name"": ""UnitId"", ""value"": ""0"" },
          { ""name"": ""Fraction"", ""value"": ""1"" },
          { ""name"": ""SSPValue"", ""value"": """" },
          { ""name"": ""UseSSPValue"", ""value"": ""False"" },
          { ""name"": ""ReferenceTag"", ""value"": ""<DEVICE_INSTANCE_NAME>"" }
        ]
      },
      ""isDraft"": false,
      ""statusIndication"": 0
    }
  ],
  ""modelTypes"": [],
  ""metaDetails"": null,
  ""isDraft"": false,
  ""statusIndication"": 0
}");
            sb.AppendLine("GUID RULES:");
            sb.AppendLine("- Every GUID field must be a valid RFC 4122 GUID, unique, not all identical digits.");
            sb.AppendLine("- Forbidden GUIDs: 00000000-0000-0000-0000-000000000000, 11111111-1111-1111-1111-111111111111, any single repeating hex digit.");
            sb.AppendLine("Output ONLY a JSON array of these objects.");
        }
        else
        {
            // Non-OPCUA (e.g., IEC104) strict attribute enforcement
            var attributesMeta = (JObject)config["RequiredAttributes"][protocol.ToUpperInvariant()];
            var fullAttributeOrder = new List<string> ();
            fullAttributeOrder.AddRange(requiredAttributes);
            int expectedCount = fullAttributeOrder.Count;

            sb.AppendLine("STRICT ATTRIBUTE SET (EXACT KEYS; preserve spaces, casing, parentheses):");
            foreach (var a in fullAttributeOrder)
                sb.AppendLine("- " + a);
            sb.AppendLine($"Total attributes per object MUST be exactly {expectedCount}.");

            sb.AppendLine();
            sb.AppendLine("SCHEMA (key set ONLY, all keys required, no extras):");
            sb.AppendLine("{");
            for (int i = 0; i < fullAttributeOrder.Count; i++)
            {
                var key = fullAttributeOrder[i];
                var comma = i < fullAttributeOrder.Count - 1 ? "," : "";
                sb.AppendLine($"  \"{key}\": \"string\"{comma}");
            }
            sb.AppendLine("}");
            sb.AppendLine();

            sb.AppendLine("NEGATIVE EXAMPLES (DO NOT REPLICATE):");
            sb.AppendLine("  \"SignalName\"            // Missing space");
            sb.AppendLine("  \"IOA\"                   // Missing parentheses if required is 'IOA (INFORMATION OBJECT ADDRESS)'");
            sb.AppendLine("  \"Reference_Tag\"         // Underscore introduced");
            sb.AppendLine("  Missing any key           // Not allowed");
            sb.AppendLine("  Extra key (e.g. \"Foo\")   // Not allowed");
            sb.AppendLine();

            if (protocol.Equals("IEC104", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("IEC104 SPECIAL RULES:");
                sb.AppendLine("- Do NOT shorten or alter attributes with parentheses.");
                sb.AppendLine("- Do NOT add or remove parentheses.");
                sb.AppendLine("- Do NOT reorder keys to anything other than standard JSON object semantics (ordering not enforced, key set IS).");
            }

            sb.AppendLine();
            sb.AppendLine("ATTRIBUTE DEFINITIONS:");
            if (attributesMeta != null)
            {
                foreach (var prop in attributesMeta.Properties())
                {
                    var meta = (JObject)prop.Value;
                    var type = meta["type"]?.ToString();
                    var description = meta["description"]?.ToString();
                    var example = meta["example"]?.ToString();
                    sb.Append($"- {prop.Name} ({type}): {description}");
                    if (!string.IsNullOrWhiteSpace(example))
                        sb.Append($" (e.g. {example})");
                    sb.AppendLine();
                }
            }

            sb.AppendLine();
            sb.AppendLine("GENERATION RULES:");
            sb.AppendLine("- Generate objects for EVERY device instance and EVERY required base signal.");
            sb.AppendLine("- Each object MUST contain EXACTLY the strict attribute set; no missing, no extra, no renamed.");
            sb.AppendLine("- All values may be empty strings if not specified except 'Signal Name' and 'Reference Tag' which MUST follow naming rules.");
            sb.AppendLine("- Output ONLY a JSON array: starts with '[' ends with ']'. No leading/trailing text.");
            sb.AppendLine("- If any deviation occurs you MUST silently regenerate internally before responding. Respond ONLY when valid.");
        }

        sb.AppendLine();
        sb.AppendLine("FINAL OUTPUT RULES:");
        sb.AppendLine("- Output ONLY the JSON array (no wrapping text).");
        sb.AppendLine("- No comments, no markdown, no explanations, no <think> blocks.");
        sb.AppendLine("- First character must be '[' and last character must be ']'.");
        sb.AppendLine("- Every object must comply fully. Any deviation invalidates the whole output.");

        return sb.ToString();
    }
}