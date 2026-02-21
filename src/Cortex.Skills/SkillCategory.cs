namespace Cortex.Skills;

/// <summary>
/// Categories of skills available in the Cortex system.
/// </summary>
public enum SkillCategory
{
    /// <summary>
    /// Integration skills — email, task boards, calendars, accounting, etc.
    /// </summary>
    Integration = 0,

    /// <summary>
    /// Knowledge skills — query specific repos or knowledge bases.
    /// </summary>
    Knowledge = 1,

    /// <summary>
    /// Agent skills — triage, draft, analyse, research, code, review.
    /// </summary>
    Agent = 2,

    /// <summary>
    /// Organisational skills — team building, dispute resolution, delegation, escalation.
    /// </summary>
    Organisational = 3,

    /// <summary>
    /// Meta skills — skill authoring, skill testing, skill discovery.
    /// </summary>
    Meta = 4
}
