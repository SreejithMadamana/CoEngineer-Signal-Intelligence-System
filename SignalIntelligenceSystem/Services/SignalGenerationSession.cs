namespace SignalIntelligenceSystem.Services
{
    public class SignalGenerationSession
    {
        // Core inputs captured during the interaction
        public string? DeviceType { get; set; }
        public int? DeviceCount { get; set; }
        public string? Protocol { get; set; }

        // Last generated raw prompt (optional diagnostics)
        public string? LastPrompt { get; set; }
        // Last response / artifacts
        public SignalResponse? LastSignalResponse { get; set; }
        // Chat transcript (if you maintain one)
        public List<(string Role, string Content)> Messages { get; } = new();
        public bool HasCompletedGeneration =>
            LastSignalResponse != null;

        public void Reset()
        {
            DeviceType = null;
            DeviceCount = null;
            Protocol = null;
            LastPrompt = null;
            LastSignalResponse = null;
            Messages.Clear();
        }
    }
}
