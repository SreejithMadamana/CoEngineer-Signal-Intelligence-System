using SignalIntelligenceSystem.Interfaces;
namespace SignalIntelligenceSystem.Services
{
    public class InputValidatorService : IInputValidatorService
    {
        public bool Validate(SignalRequest request, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(request.DeviceType))
            {
                errorMessage = "DeviceType is required.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(request.Protocol))
            {
                errorMessage = "Protocol is required.";
                return false;
            }
            return true;
        }
    }
}
