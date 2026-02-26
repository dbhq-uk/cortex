namespace Cortex.Core.Context;

/// <summary>
/// Classification categories for business context entries.
/// </summary>
public enum ContextCategory
{
    /// <summary>Notes about a specific customer or client.</summary>
    CustomerNote,

    /// <summary>Notes from meetings or discussions.</summary>
    MeetingNote,

    /// <summary>Recorded decisions and their rationale.</summary>
    Decision,

    /// <summary>Lessons learned from past work.</summary>
    Lesson,

    /// <summary>Preferences for how work should be done.</summary>
    Preference,

    /// <summary>High-level strategic context.</summary>
    Strategic,

    /// <summary>Day-to-day operational context.</summary>
    Operational
}
