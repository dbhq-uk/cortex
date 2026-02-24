namespace Cortex.Agents.Tests;

public sealed class InMemoryAgentRegistryTests
{
    private readonly InMemoryAgentRegistry _registry = new();

    private static AgentRegistration CreateRegistration(
        string agentId = "test-agent",
        string name = "Test Agent",
        string agentType = "ai",
        params AgentCapability[] capabilities) =>
        new()
        {
            AgentId = agentId,
            Name = name,
            AgentType = agentType,
            Capabilities = capabilities,
            RegisteredAt = DateTimeOffset.UtcNow
        };

    [Fact]
    public async Task RegisterAsync_NullRegistration_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _registry.RegisterAsync(null!));
    }

    [Fact]
    public async Task RegisterAsync_ThenFindById_ReturnsRegistration()
    {
        var reg = CreateRegistration("agent-1");
        await _registry.RegisterAsync(reg);

        var result = await _registry.FindByIdAsync("agent-1");

        Assert.NotNull(result);
        Assert.Equal("agent-1", result.AgentId);
    }

    [Fact]
    public async Task FindByIdAsync_NotFound_ReturnsNull()
    {
        var result = await _registry.FindByIdAsync("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateId_OverwritesRegistration()
    {
        await _registry.RegisterAsync(CreateRegistration("agent-1", name: "First"));
        await _registry.RegisterAsync(CreateRegistration("agent-1", name: "Second"));

        var result = await _registry.FindByIdAsync("agent-1");

        Assert.NotNull(result);
        Assert.Equal("Second", result.Name);
    }

    [Fact]
    public async Task FindByCapabilityAsync_MatchesCapabilityName()
    {
        var cap = new AgentCapability { Name = "drafting", Description = "Draft documents" };
        await _registry.RegisterAsync(CreateRegistration("agent-1", capabilities: cap));
        await _registry.RegisterAsync(CreateRegistration("agent-2"));

        var results = await _registry.FindByCapabilityAsync("drafting");

        Assert.Single(results);
        Assert.Equal("agent-1", results[0].AgentId);
    }

    [Fact]
    public async Task FindByCapabilityAsync_NoMatch_ReturnsEmpty()
    {
        await _registry.RegisterAsync(CreateRegistration("agent-1"));

        var results = await _registry.FindByCapabilityAsync("nonexistent");

        Assert.Empty(results);
    }

    [Fact]
    public async Task FindByCapabilityAsync_OnlyReturnsAvailableAgents()
    {
        var cap = new AgentCapability { Name = "drafting", Description = "Draft documents" };
        await _registry.RegisterAsync(CreateRegistration("agent-1", capabilities: cap));
        await _registry.RegisterAsync(new AgentRegistration
        {
            AgentId = "agent-2",
            Name = "Unavailable",
            AgentType = "ai",
            Capabilities = [cap],
            RegisteredAt = DateTimeOffset.UtcNow,
            IsAvailable = false
        });

        var results = await _registry.FindByCapabilityAsync("drafting");

        Assert.Single(results);
        Assert.Equal("agent-1", results[0].AgentId);
    }
}
