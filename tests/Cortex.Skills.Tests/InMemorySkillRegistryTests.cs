namespace Cortex.Skills.Tests;

public sealed class InMemorySkillRegistryTests
{
    private readonly InMemorySkillRegistry _registry = new();

    private static SkillDefinition CreateDefinition(
        string skillId = "test-skill",
        string name = "Test Skill",
        SkillCategory category = SkillCategory.Agent,
        string executorType = "csharp") =>
        new()
        {
            SkillId = skillId,
            Name = name,
            Description = $"Description for {name}",
            Category = category,
            ExecutorType = executorType
        };

    [Fact]
    public async Task RegisterAsync_NullDefinition_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _registry.RegisterAsync(null!));
    }

    [Fact]
    public async Task RegisterAsync_ThenFindById_ReturnsDefinition()
    {
        var def = CreateDefinition("triage-skill");
        await _registry.RegisterAsync(def);

        var result = await _registry.FindByIdAsync("triage-skill");

        Assert.NotNull(result);
        Assert.Equal("triage-skill", result.SkillId);
    }

    [Fact]
    public async Task FindByIdAsync_NotFound_ReturnsNull()
    {
        var result = await _registry.FindByIdAsync("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateId_OverwritesDefinition()
    {
        await _registry.RegisterAsync(CreateDefinition("skill-1", name: "First"));
        await _registry.RegisterAsync(CreateDefinition("skill-1", name: "Second"));

        var result = await _registry.FindByIdAsync("skill-1");

        Assert.NotNull(result);
        Assert.Equal("Second", result.Name);
    }

    [Fact]
    public async Task SearchAsync_MatchesName()
    {
        await _registry.RegisterAsync(CreateDefinition("s1", name: "Email Triage"));
        await _registry.RegisterAsync(CreateDefinition("s2", name: "Code Review"));

        var results = await _registry.SearchAsync("email");

        Assert.Single(results);
        Assert.Equal("s1", results[0].SkillId);
    }

    [Fact]
    public async Task SearchAsync_MatchesDescription()
    {
        await _registry.RegisterAsync(CreateDefinition("s1", name: "Triage"));

        var results = await _registry.SearchAsync("Description for Triage");

        Assert.Single(results);
    }

    [Fact]
    public async Task SearchAsync_NoMatch_ReturnsEmpty()
    {
        await _registry.RegisterAsync(CreateDefinition("s1"));

        var results = await _registry.SearchAsync("zzz-no-match");

        Assert.Empty(results);
    }

    [Fact]
    public async Task FindByCategoryAsync_FiltersCorrectly()
    {
        await _registry.RegisterAsync(CreateDefinition("s1", category: SkillCategory.Agent));
        await _registry.RegisterAsync(CreateDefinition("s2", category: SkillCategory.Integration));

        var results = await _registry.FindByCategoryAsync(SkillCategory.Agent);

        Assert.Single(results);
        Assert.Equal("s1", results[0].SkillId);
    }

    [Fact]
    public async Task FindByCategoryAsync_NoMatch_ReturnsEmpty()
    {
        await _registry.RegisterAsync(CreateDefinition("s1", category: SkillCategory.Agent));

        var results = await _registry.FindByCategoryAsync(SkillCategory.Meta);

        Assert.Empty(results);
    }
}
