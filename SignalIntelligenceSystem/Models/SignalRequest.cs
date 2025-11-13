public class SignalRequest
{
    public string DeviceType { get; set; }
    public int DeviceCount { get; set; }
    public string Protocol { get; set; }    
    public int SignalCount { get; set; }
    public string PromptOverride { get; set; } // Optional
    public List<string> FeedbackSignals { get; set; } = new(); // Add this
}