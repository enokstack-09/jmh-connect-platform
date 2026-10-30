# JMH Connect — AI Agent Rules

## 1. Understand Before Acting
- Read MASTER_CONTEXT.md before starting project work.
- Read relevant Second Brain documents before proposing changes.
- Inspect the existing repository before making implementation decisions.
- Do not assume that planned features already exist.

## 2. Second Brain Change Control
- Treat docs/second-brain as read-only by default.
- Edit these documents only when the user explicitly requests a Second Brain update.
- Keep updates evidence-based and limited to verified implementation, test results and approved decisions.
- Never rename, move or delete Second Brain files without explicit approval.
- Do not attempt to bypass filesystem permissions or read-only restrictions.

## 3. Change Control
- Do not modify source code unless the user has requested implementation or approved the specific work.
- Prefer small, focused changes over unnecessary rewrites.
- Preserve the existing architecture unless a justified change is approved.
- Explain which files need changing and why.
- Ask before destructive operations, database resets, migrations that may affect data, or dependency upgrades.

## 4. Security
- Never expose or commit credentials, passwords, API keys or secrets.
- Never put database credentials in frontend code.
- Enforce authorization on the backend.
- Do not invent role permissions or private-data access rules.
- Respect record-level access and privacy requirements.

## 5. UI Rules
- Follow the approved JMH palette:
  - Primary: #464B71
  - Secondary: #118AB2
  - Accent: #7CD5C7
  - Background: #F2F2ED
- Aim for a classical, elegant, premium and responsive UI.
- Do not introduce extra accent colors without approval.

## 6. Coding Workflow
1. Understand the requested task.
2. Read relevant documentation.
3. Inspect the current implementation.
4. Propose a minimal implementation plan.
5. Make only authorized changes.
6. Run appropriate tests or checks.
7. Report changed files, test results and remaining risks.
8. Update documentation only when the user explicitly requests it.

## 7. Truthfulness
- Do not claim tests passed unless they were run successfully.
- Do not claim a feature is implemented without verification.
- Clearly identify assumptions, conflicts and missing requirements.
- Ask the user when a business decision is unclear.

## 8. MCP Security Boundary
- Use only the explicitly approved Second Brain directory for the MCP filesystem connection.
- The MCP filesystem mount must be enforced as read-only by the operating system or container, not only by these instructions.
- Do not expose the whole project directory through this MCP server.
- Do not assume these rules alone can restrict other tools available to the agent.
