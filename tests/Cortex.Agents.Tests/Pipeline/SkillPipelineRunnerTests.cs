using Cortex.Agents.Pipeline;
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Skills;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Agents.Tests.Pipeline;

public sealed class SkillPipelineRunnerTests
{
    private readonly InMemorySkillRegistry _skillRegistry = new();
    private readonly FakeSkillExecutor _fakeExecutor = new("fake");

    private SkillPipelineRunner CreateRunner(params ISkillExecutor[] executors)
    {
        var allExecutors = executors.Length > 0 ? executors : [_fakeExecutor];
        return new SkillPipelineRunner(
            _skillRegistry,
            allExecutors,
            NullLogger<SkillPipelineRunner>.Instance);
    }

    private static MessageEnvelope CreateEnvelope(string content = "test") =>
        new()
        {
            Message = new TestMessage { Content = content },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1)
        };

    private SkillDefinition RegisterSkill(
        string skillId = "test-skill",
        string executorType = "fake")
    {
        var def = new SkillDefinition
        {
            SkillId = skillId,
            Name = skillId,
            Description = $"Test skill {skillId}",
            Category = SkillCategory.Agent,
            ExecutorType = executorType
        };
        _skillRegistry.RegisterAsync(def).GetAwaiter().GetResult();
        return def;
    }

    [Fact]
    public async Task RunAsync_EmptyPipeline_ReturnsContextWithNoResults()
    {
        var runner = CreateRunner();
        var envelope = CreateEnvelope();

        var context = await runner.RunAsync([], envelope);

        Assert.Same(envelope, context.Envelope);
        Assert.Empty(context.Results);
    }

    [Fact]
    public async Task RunAsync_SingleSkill_ExecutesAndStoresResult()
    {
        RegisterSkill("triage");
        _fakeExecutor.SetResult("triage", "triage-output");
        var runner = CreateRunner();

        var context = await runner.RunAsync(["triage"], CreateEnvelope());

        Assert.Single(context.Results);
        Assert.Equal("triage-output", context.Results["triage"]);
    }

    [Fact]
    public async Task RunAsync_MultipleSkills_ExecutesInOrder()
    {
        RegisterSkill("skill-a");
        RegisterSkill("skill-b");
        _fakeExecutor.SetResult("skill-a", "output-a");
        _fakeExecutor.SetResult("skill-b", "output-b");
        var runner = CreateRunner();

        var context = await runner.RunAsync(["skill-a", "skill-b"], CreateEnvelope());

        Assert.Equal(2, context.Results.Count);
        Assert.Equal("output-a", context.Results["skill-a"]);
        Assert.Equal("output-b", context.Results["skill-b"]);

        // Verify execution order
        Assert.Equal("skill-a", _fakeExecutor.Calls[0].SkillId);
        Assert.Equal("skill-b", _fakeExecutor.Calls[1].SkillId);
    }

    [Fact]
    public async Task RunAsync_LaterSkillReceivesPriorResults()
    {
        RegisterSkill("skill-a");
        RegisterSkill("skill-b");
        _fakeExecutor.SetResult("skill-a", "output-a");
        var runner = CreateRunner();

        await runner.RunAsync(["skill-a", "skill-b"], CreateEnvelope());

        // Second skill should receive first skill's result in the parameters
        var secondCallParams = _fakeExecutor.Calls[1].Parameters;
        var results = (Dictionary<string, object?>)secondCallParams["results"];
        Assert.Equal("output-a", results["skill-a"]);
    }

    [Fact]
    public async Task RunAsync_UnknownSkill_SkipsWithoutError()
    {
        RegisterSkill("known");
        _fakeExecutor.SetResult("known", "known-output");
        var runner = CreateRunner();

        var context = await runner.RunAsync(["unknown", "known"], CreateEnvelope());

        Assert.Single(context.Results);
        Assert.Equal("known-output", context.Results["known"]);
    }

    [Fact]
    public async Task RunAsync_NoMatchingExecutor_SkipsWithoutError()
    {
        var def = new SkillDefinition
        {
            SkillId = "orphan",
            Name = "Orphan",
            Description = "No executor for this type",
            Category = SkillCategory.Agent,
            ExecutorType = "nonexistent-executor"
        };
        await _skillRegistry.RegisterAsync(def);
        var runner = CreateRunner();

        var context = await runner.RunAsync(["orphan"], CreateEnvelope());

        Assert.Empty(context.Results);
    }

    [Fact]
    public async Task RunAsync_AdditionalParameters_AvailableToSkills()
    {
        RegisterSkill("triage");
        var runner = CreateRunner();
        var extraParams = new Dictionary<string, object>
        {
            ["availableCapabilities"] = "email-drafting, code-review"
        };

        await runner.RunAsync(["triage"], CreateEnvelope(), extraParams);

        var callParams = _fakeExecutor.Calls[0].Parameters;
        Assert.Equal("email-drafting, code-review", callParams["availableCapabilities"]);
    }

    [Fact]
    public async Task RunAsync_EnvelopePassedInParameters()
    {
        RegisterSkill("triage");
        var runner = CreateRunner();
        var envelope = CreateEnvelope("hello world");

        await runner.RunAsync(["triage"], envelope);

        var callParams = _fakeExecutor.Calls[0].Parameters;
        Assert.Same(envelope, callParams["envelope"]);
    }
}
