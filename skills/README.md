# Skills

Skills are the universal unit of capability in Cortex. Each skill is a markdown file describing what it does, when to use it, and how to execute it.

## Skill Structure

A skill definition file contains:

- **Name** — what the skill is called
- **Description** — what it does
- **Triggers** — when to activate it
- **Executor type** — how to run it (C#, Python, CLI, API)
- **Instructions** — step-by-step execution guide

## Skill Categories

| Category        | Description                                              |
|-----------------|----------------------------------------------------------|
| Integration     | Email, task boards, calendars, accounting, etc.          |
| Knowledge       | Query specific repos or knowledge bases                  |
| Agent           | Triage, draft, analyse, research, code, review           |
| Organisational  | Team building, dispute resolution, delegation            |
| Meta            | Skill authoring, skill testing, skill discovery          |

## Creating Skills

Skills can be authored by humans or by builder agents. See the [vision document](../docs/architecture/vision.md) for the full skill philosophy.
