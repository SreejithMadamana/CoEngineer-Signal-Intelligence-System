public class LLMParsedRequest
{
    public string DeviceType { get; set; }
    public string Protocol { get; set; }
    public int DeviceCount { get; set; }
    public string ControlMethod { get; set; }
    public List<string> FeedbackSignals { get; set; }
}
