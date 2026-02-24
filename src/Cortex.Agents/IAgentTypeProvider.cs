namespace Cortex.Agents;

/// <summary>
/// Optional interface for agents to declare their type ("human" or "ai").
/// </summary>
public interface IAgentTypeProvider
{
    /// <summary>
    /// The agent type, typically "human" or "ai".
    /// </summary>
    string AgentType { get; }
}
