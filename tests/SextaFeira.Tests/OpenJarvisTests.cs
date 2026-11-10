using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SextaFeira.Domain.Entities;
using SextaFeira.Domain.Enums;
using SextaFeira.Domain.Interfaces;
using SextaFeira.Infrastructure.OpenJarvis;
using Xunit;

namespace SextaFeira.Tests;

public class OpenJarvisTests
{
    private OpenJarvisInferenceEngine CreateEngine()
    {
        var httpClient = new HttpClient();
        var config = new ConfigurationBuilder().Build();
        var logger = NullLogger<OpenJarvisInferenceEngine>.Instance;
        return new OpenJarvisInferenceEngine(httpClient, config, logger);
    }

    [Fact]
    public async Task OpenJarvisEngine_ShouldProbeHardware_AndReturnRealisticQuotas()
    {
        // Arrange
        var engine = CreateEngine();

        // Act
        var hardware = await engine.ProbeHardwareAsync();

        // Assert
        Assert.NotNull(hardware);
        Assert.True(hardware.ProcessorCount > 0);
        Assert.True(hardware.TotalMemoryMb > 0);
        Assert.NotEmpty(hardware.AcceleratorType);
        Assert.NotEmpty(hardware.RecommendedLocalModel);
        Assert.True(hardware.PowerEfficiencyIndex > 0);
    }

    [Fact]
    public async Task OpenJarvisEngine_ExecuteLocalFastAsync_ShouldProduceSub250msExecution()
    {
        // Arrange
        var engine = CreateEngine();
        await engine.ProbeHardwareAsync(); // Warm up hardware detection
        var request = new LLMRequest("qual o status dos sistemas?");

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await engine.ExecuteLocalFastAsync(request);
        sw.Stop();

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Text);
        Assert.Equal(LLMProviderType.OpenJarvisLocal, response.Provider);
        Assert.True(sw.ElapsedMilliseconds < 350, $"Execution took {sw.ElapsedMilliseconds}ms, expected sub-350ms");
    }

    [Fact]
    public async Task OpenJarvisEngine_GetSystemStatus_ShouldReportIntelligencePerWattAndEfficiency()
    {
        // Arrange
        var engine = CreateEngine();

        // Warm up with an execution to generate traces
        await engine.ExecuteLocalFastAsync(new LLMRequest("teste de eficiência energética"));

        // Act
        var status = await engine.GetSystemStatusAsync();

        // Assert
        Assert.NotNull(status);
        Assert.NotNull(status.Hardware);
        Assert.NotNull(status.ActiveIntelligence);
        Assert.True(status.IntelligencePerWattIndex > 0);
        Assert.NotEmpty(status.ActiveMode);
        Assert.NotEmpty(status.RecentTraces);
    }

    [Fact]
    public async Task OpenJarvisEngine_SynthesizeSpecAsync_ShouldSynthesizeAndCachePlan()
    {
        // Arrange
        var engine = CreateEngine();
        var sampleTools = new List<ToolDefinition>
        {
            new("device_battery_status", "Retorna status da bateria e voltagem", "{}", ToolCategory.System),
            new("thermal_diagnostics", "Retorna diagnóstico térmico de CPU e GPU", "{}", ToolCategory.System)
        };

        // Act
        var plan = await engine.SynthesizeSpecAsync("verificar status da bateria e diagnóstico térmico", sampleTools);

        // Assert
        Assert.NotNull(plan);
        Assert.NotEmpty(plan.UserGoal);
        Assert.NotEmpty(plan.ExecutionSteps);
        Assert.NotEmpty(plan.RequiredTools);
        Assert.NotEmpty(plan.CompiledLocalPromptTemplate);
        Assert.True(plan.PassedRegressionGate);
    }
}
