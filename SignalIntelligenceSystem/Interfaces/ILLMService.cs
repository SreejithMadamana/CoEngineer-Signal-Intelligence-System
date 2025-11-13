public interface ILLMService
{
    Task<string> GetLLMResponseAsync(string prompt);
}