using Microsoft.AspNetCore.Mvc;
using SignalIntelligenceSystem.Services;
using System.Text;
[ApiController]
[Route("api/[controller]")]
public class SignalController : ControllerBase
{
    private readonly SignalOrchestratorService _orchestrator;
    private readonly SignalGenerationSession _session;
    public SignalController(SignalOrchestratorService orchestrator, SignalGenerationSession session)
    {
        _session = session;
        _orchestrator = orchestrator;
    }
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] SignalRequest request)
    {
        var (success, error, response) = await _orchestrator.GenerateArtifactsAsync(request);

        if (!success)
            return BadRequest(new { error });
        // Persist inputs
        _session.DeviceType = request.DeviceType;
        _session.DeviceCount = request.DeviceCount;
        _session.Protocol = request.Protocol;
        _session.LastSignalResponse = response;
        return Ok(response);
    }   

    [HttpPost("download/csv")]
    public async Task<IActionResult> DownloadCsv([FromBody] SignalRequest request)
    {
        var (success, error, response) = await _orchestrator.GenerateArtifactsAsync(request);

        if (!success)
            return BadRequest(new { error });

        var bytes = Encoding.UTF8.GetBytes(response.CsvContent);
        return File(bytes, "text/csv", "SignalList.csv");
    }
      
    [HttpPost("download/json")]
    public async Task<IActionResult> DownloadJson([FromBody] SignalRequest request)
    {
        var (success, error, response) = await _orchestrator.GenerateArtifactsAsync(request);

        if (!success)
            return BadRequest(new { error });

        var bytes = Encoding.UTF8.GetBytes(response.JsonContent);
        return File(bytes, "application/json", "SignalList.json");
    }
}