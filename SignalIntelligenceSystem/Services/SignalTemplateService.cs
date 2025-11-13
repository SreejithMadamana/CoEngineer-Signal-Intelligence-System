namespace SignalIntelligenceSystem.Services
{
    using SignalIntelligenceSystem.Models;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Linq;
    public class SignalTemplateService
    {
        private readonly Dictionary<string, DeviceTemplate> _templates;
        private readonly Dictionary<string, List<string>> _requiredAttributes;
        private readonly Dictionary<string, string> _excelColumnNames;
        private readonly Dictionary<string, JsonObject> _attributeMetadata; // NEW: For attribute metadata

        public SignalTemplateService(string jsonPath)
        {
            var json = File.ReadAllText(jsonPath);
            var doc = JsonNode.Parse(json);

            // Parse device templates
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _templates = JsonSerializer.Deserialize<Dictionary<string, DeviceTemplate>>(json, options);

            // Parse required attributes (support both old array and new object format)
            _requiredAttributes = new();
            _attributeMetadata = new();
            if (doc?["RequiredAttributes"] is JsonObject requiredAttrs)
            {
                foreach (var protocol in requiredAttrs)
                {
                    if (protocol.Value is JsonArray arr)
                    {
                        // Old format: array of strings
                        _requiredAttributes[protocol.Key] = arr.Select(x => x?.ToString()).Where(x => x != null).ToList()!;
                    }
                    else if (protocol.Value is JsonObject obj)
                    {
                        // New format: object with attribute metadata
                        _requiredAttributes[protocol.Key] = obj.Select(x => x.Key).ToList();
                        _attributeMetadata[protocol.Key] = obj;
                    }
                }
            }

            // Parse Excel column names
            _excelColumnNames = new();
            if (doc?["ExcelColumnNames"] is JsonObject colNames)
            {
                foreach (var col in colNames)
                {
                    _excelColumnNames[col.Key] = col.Value?.ToString() ?? col.Key;
                }
            }
        }

        public DeviceTemplate GetDeviceTemplate(string deviceType)
        {
            if (_templates.TryGetValue(deviceType, out var template))
                return template;
            throw new KeyNotFoundException($"Device type '{deviceType}' not found in signal templates.");
        }

        public string GetExcelColumnName(string attribute)
        {
            if (_excelColumnNames.TryGetValue(attribute, out var colName))
                return colName;
            return attribute; // fallback to attribute name
        }

        public List<string> GetRequiredAttributes(string protocol)
        {
            if (_requiredAttributes.TryGetValue(protocol, out var attrs))
                return attrs;
            return new List<string>();
        }

        // NEW: Get attribute metadata for a protocol (returns null if not found)
        public JsonObject? GetAttributeMetadata(string protocol)
        {
            if (_attributeMetadata.TryGetValue(protocol, out var meta))
                return meta;
            return null;
        }

        // NEW: Get metadata for a specific attribute in a protocol (returns null if not found)
        public JsonObject? GetAttributeMetadata(string protocol, string attribute)
        {
            if (_attributeMetadata.TryGetValue(protocol, out var meta) && meta[attribute] is JsonObject attrMeta)
                return attrMeta;
            return null;
        }

        // NEW: Get all device type keys
        public List<string> GetDeviceTemplateKeys()
        {
            return _templates.Keys.ToList();
        }

        // NEW: Get all unique protocol names from all templates
        public List<string> GetAllProtocols()
        {
            var protocols = new HashSet<string>();
            foreach (var template in _templates.Values)
            {
                if (template.ProtocolDefaults != null)
                {
                    foreach (var proto in template.ProtocolDefaults.Keys)
                    {
                        protocols.Add(proto);
                    }
                }
            }
            return protocols.ToList();
        }
        public List<AggregatedSignal> GetAllSignals(string deviceType)
        {
            var result = new Dictionary<string, AggregatedSignal>(StringComparer.OrdinalIgnoreCase);
            if (!_templates.TryGetValue(deviceType, out var template))
                return new List<AggregatedSignal>(); // or throw

            void Add(IEnumerable<SignalDefinition>? defs, string sourceCategory, string? group = null)
            {
                if (defs == null) return;
                foreach (var d in defs)
                {
                    if (d == null || string.IsNullOrWhiteSpace(d.SIG_NAME)) continue;
                    if (!result.ContainsKey(d.SIG_NAME))
                    {
                        result[d.SIG_NAME] = new AggregatedSignal
                        {
                            SIG_NAME = d.SIG_NAME,
                            SIG_DESC = d.SIG_DESC,
                            SIG_TYPE = d.SIG_TYPE,
                            Source = sourceCategory,
                            Group = group
                        };
                    }
                }
            }

            // Base signals
            Add(template.Default?.BaseSignals, "Base");

            // Control methods
            if (template.ControlMethods != null)
                foreach (var kv in template.ControlMethods)
                    Add(kv.Value, "ControlMethod", kv.Key);

            // Feedback signals (Dictionary<string, SignalDefinition>)
            if (template.FeedbackSignals != null)
                foreach (var kv in template.FeedbackSignals)
                    Add(new[] { kv.Value }, "Feedback", kv.Key);

            // SensorTypes (only for SENSOR template)
            if (template.SensorTypes != null)
                foreach (var kv in template.SensorTypes)
                    Add(kv.Value, "SensorType", kv.Key);

            return result.Values
                         .OrderBy(s => s.Source)
                         .ThenBy(s => s.Group)
                         .ThenBy(s => s.SIG_NAME, StringComparer.OrdinalIgnoreCase)
                         .ToList();
        }
    }
    
  
    }
public class AggregatedSignal
{
    public string SIG_NAME { get; set; } = "";
    public string SIG_DESC { get; set; } = "";
    public string SIG_TYPE { get; set; } = "";
    public string Source { get; set; } = ""; // Base / ControlMethod / Feedback / SensorType
    public string? Group { get; set; }       // e.g. VFD, Temperature, Pressure
}