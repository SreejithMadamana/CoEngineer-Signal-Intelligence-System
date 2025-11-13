using SignalIntelligenceSystem.Services;
public interface IFileGeneratorService
{    SignalResponse GenerateFiles(
        List<Dictionary<string, object>> signals, SignalTemplateService signalTemplateService);  
}