
    namespace SignalIntelligenceSystem.Services
    {
        public class DeviceTypeOptions
        {
            public List<string> ControlMethods { get; set; } = new();
            public List<string> FeedbackSignals { get; set; } = new();
            public List<string> SensorTypes { get; set; } = new();
        }
    }

