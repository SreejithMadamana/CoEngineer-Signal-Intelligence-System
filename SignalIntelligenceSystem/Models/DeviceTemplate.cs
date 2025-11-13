namespace SignalIntelligenceSystem.Models
{
    public class DeviceTemplate
    {
        public DefaultTemplate Default { get; set; }
        public Dictionary<string, List<SignalDefinition>> ControlMethods { get; set; }
        public Dictionary<string, SignalDefinition> FeedbackSignals { get; set; }
        public Dictionary<string, ProtocolDefaults> ProtocolDefaults { get; set; }
        public Dictionary<string, List<SignalDefinition>> SensorTypes { get; set; } // <-- Add this line
    }
}
