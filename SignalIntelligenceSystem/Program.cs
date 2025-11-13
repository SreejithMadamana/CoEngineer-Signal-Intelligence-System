using Microsoft.AspNetCore.Mvc;

using SignalIntelligenceSystem.Interfaces;
using SignalIntelligenceSystem.Services;

using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddSingleton<SignalTemplateService>(provider =>
    new SignalTemplateService("Config/signal_templates.json"));
builder.Services.AddScoped<IInputValidatorService, InputValidatorService>();
//builder.Services.AddHttpClient<ILLMService, LLMService>();
builder.Services.AddScoped<IResponseParserService, ResponseParserService>();
builder.Services.AddScoped<IFileGeneratorService, FileGeneratorService>();
builder.Services.AddScoped<SignalOrchestratorService>();
builder.Services.AddScoped<OllamaService>();
builder.Services.AddScoped<SignalModelService>(provider =>
    new SignalModelService());
//builder.Services.AddSingleton<IPromptBuilderService>(provider =>
//    new PromptBuilderService());
builder.Services.AddScoped<SignalGenerationSession>();
//builder.Services.AddSingleton<DeviceOptionsService>();
builder.Services.AddSingleton<ILLMService>(sp =>
    new GroqService("")// add your token here.
);
// Add CORS if needed for Blazor WASM
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazor",
        policy => policy.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Signal Intelligence API",
        Version = "v1",
        Description = "Agentic AI-based Signal Engineering Artifact Generator"
    });
});
builder.Services.AddControllers();

var app = builder.Build();

// Enable Swagger in Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowBlazor");
app.UseHttpsRedirection();
app.MapControllers();

// In-memory store for session results (for demonstration)
var results = new ConcurrentDictionary<string, string>();

// POST /api/chat/message
//app.MapPost("/api/chat/message", async (HttpRequest request, [FromQuery] string sessionId) =>
//{
//    using var reader = new StreamReader(request.Body);
//    var userText = await reader.ReadToEndAsync();

//    // Simulate processing and delayed result
//    if (userText.Contains("signal", StringComparison.OrdinalIgnoreCase))
//    {
//        // Simulate async processing by storing a placeholder
//        results[sessionId] = null;
//        _ = Task.Run(async () =>
//        {
//            await Task.Delay(5000); // Simulate work
//            results[sessionId] = "{ \"signals\": [\"A\", \"B\", \"C\"] }";
//        });

//        return Results.Json(new
//        {
//            response = "Generating signal list, please wait...",
//            session = sessionId
//        });
//    }

//    return Results.Json(new
//    {
//        response = $"Echo: {userText}",
//        session = sessionId
//    });
//});

// GET /api/chat/result
//app.MapGet("/api/chat/result", ([FromQuery] string sessionId) =>
//{
//    if (results.TryGetValue(sessionId, out var result) && result != null)
//    {
//        return Results.Json(new { ready = true, result });
//    }
//    return Results.Json(new { ready = false, result = "" });
//});



app.Run();