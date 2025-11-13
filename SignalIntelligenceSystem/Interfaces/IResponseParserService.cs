public interface IResponseParserService
{
    List<SignalItem> Parse(string llmOutput);
    List<SignalDefinition> ParseWithMetadata(string llmOutput, string protocol);
}