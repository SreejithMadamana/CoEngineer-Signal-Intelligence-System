namespace SignalIntelligenceSystem.Interfaces
{
    public interface IInputValidatorService
    {
        bool Validate(SignalRequest request, out string errorMessage);
    }
}
