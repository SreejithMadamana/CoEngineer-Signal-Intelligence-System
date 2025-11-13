namespace SignalIntelligenceSystem.Models
{
    public class DeviceSelection
    {
        public string DeviceType { get; set; }
        public string ControlMethod { get; set; }
        public List<string> FeedbackSignals { get; set; } = new();
        public List<string> SensorTypes { get; set; } = new();
    }
}
