namespace SignalIntelligenceSystem.Services
{
    public class SignalModelService
    {
        private Dictionary<string, List<string>> _protocolModels;
        public SignalModelService()
        {        
        }     
        public List<string> GetExpectedFields(string protocol)
        {
            if (_protocolModels == null || !_protocolModels.ContainsKey(protocol))
                throw new ArgumentException($"Unsupported protocol: {protocol}");

            return _protocolModels[protocol];
        }
        // New overload: uses mapping attributes for validation
        public List<Dictionary<string, object>> ValidateAndParseSignals(
            string protocol,
            List<Dictionary<string, object>> llmOutput,
            IEnumerable<MappingXmlParser.MappingAttribute> mappingAttributes)
        {
            var expectedFields = mappingAttributes.Select(a => a.Name).ToList();
            var validSignals = new List<Dictionary<string, object>>();

            foreach (var signal in llmOutput)
            {      
                {
                    var parsedSignal = new Dictionary<string, object>();
                    foreach (var field in expectedFields)
                    {
                        parsedSignal[field] = signal[field];
                    }
                    validSignals.Add(parsedSignal);
                }
            }

            return validSignals;
        }

        // Old method for backward compatibility
        public List<Dictionary<string, object>> ValidateAndParseSignals(string protocol, List<Dictionary<string, object>> llmOutput)
        {
            var expectedFields = GetExpectedFields(protocol);
            var validSignals = new List<Dictionary<string, object>>();

            foreach (var signal in llmOutput)
            {
                bool isValid = true;
                foreach (var field in expectedFields)
                {
                    if (!signal.ContainsKey(field) || signal[field] == null || string.IsNullOrWhiteSpace(signal[field].ToString()))
                    {
                        isValid = false;
                        break;
                    }
                }
                if (isValid)
                {
                    var parsedSignal = new Dictionary<string, object>();
                    foreach (var field in expectedFields)
                    {
                        parsedSignal[field] = signal[field];
                    }
                    validSignals.Add(parsedSignal);
                }
            }

            return validSignals;
        }
    }
}