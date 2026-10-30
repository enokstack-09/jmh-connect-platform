# JMH Connect — Decisions Log

## Purpose
Record important project decisions, their reasoning, status and consequences.
Do not treat a proposed decision as approved unless its status says Approved.

## Decision Status Values
- Proposed
- Approved
- Rejected
- Superseded

## D-001 — Frontend Framework
Status: Proposed

Decision:
Use Angular with TypeScript for the frontend.

Reason:
It matches the intended development stack and supports structured, maintainable application development.

Consequence:
Inspect the existing repository before creating or changing the frontend project.

## D-002 — Backend Framework
Status: Proposed

Decision:
Use ASP.NET Core Web API with controllers.

Reason:
It matches the intended .NET development stack and provides a structured API approach.

Consequence:
Inspect existing project conventions before deciding the final code organization.

## D-003 — Database Technology
Status: Proposed

Decision:
Use Microsoft SQL Server with Entity Framework Core.

Reason:
It matches the intended stack and supports relational data, constraints and controlled schema changes.

Consequence:
Finalize the data model before implementing schema changes.

## D-004 — Local-First Development
Status: Proposed

Decision:
Develop and test locally before selecting a cloud hosting solution.

Reason:
Avoid premature hosting costs and validate requirements before deployment.

Consequence:
Review hosting eligibility, security and pricing before adopting a paid service.

## D-005 — Second Brain as Project Context
Status: Approved

Decision:
Use docs/second-brain as the central location for project context and planning documentation.

Reason:
Keep requirements, architecture, design rules and decisions organized for consistent work across sessions.

Consequence:
Read the relevant documents before planning or implementing related changes.

## D-006 — Read-Only Second Brain for MCP
Status: Proposed

Decision:
Expose only Z:\JMH\docs\second-brain through a read-only MCP filesystem mount.

Reason:
Reduce the risk of an agent changing project documentation through that MCP connection.

Consequence:
Verify the actual mount and its permissions before relying on the restriction. MCP restrictions do not automatically limit other Codex tools.

## D-007 — UI Color Palette
Status: Approved

Decision:
Use the four approved accent colors:
- Primary Slate Indigo: #464B71
- Secondary Ocean Blue: #118AB2
- Accent Mint Teal: #7CD5C7
- Background Warm Ivory: #F2F2ED

Reason:
Maintain a consistent and recognizable premium visual identity.

Consequence:
Follow UI_DESIGN_SYSTEM.md and do not introduce additional accent colors without approval.

## D-008 — V1 Feature Scope
Status: Proposed

Decision:
Plan V1 around authentication, member management, dashboard, devotion sessions, attendance, duties, events, birthdays, announcements and basic reports.

Reason:
Prioritize essential community-management workflows.

Consequence:
Confirm stakeholder needs and role permissions before treating the scope as final.

## D-009 — Authentication Transport and Roles
Status: Approved

Decision:
Use ASP.NET Core Identity cookie sessions for the Angular SPA and API. Production is same-origin HTTPS under `/api`; local development uses Angular at `http://localhost:4200` and the API at `http://localhost:5080`. The only application roles are Pastor, Leader, and Member. Pastor is the system administrator; no Admin role exists. Public registration is disabled and only Pastor may create login accounts.

Reason:
This supports secure browser sessions with HttpOnly/Secure/SameSite cookies, credentialed allowlisted CORS for development, antiforgery protection, and server-side role authorization.

Consequence:
Production requires HTTPS, a configured origin allowlist, persistent protected data keys, and deployment settings. First-Pastor creation remains a separate one-time Development-only operation requiring explicit approval.

## D-010 — SQL Server Authentication Integration Tests
Status: Approved

Decision:
SQL Server integration tests use fixture-generated `JMHConnectBootstrapIntegration_<guid>` databases only. The fixture requires a local encrypted Windows Authentication server-only connection, records ownership, validates database identity, and deletes only a verified fixture-owned database.

Reason:
Validate real SQL Server transaction rollback, migration checks, application locking, and role assignment without using `JMHConnectDev`.

Consequence:
The Codex execution environment could not establish the required encrypted connection, but all six tests subsequently passed individually from local Windows PowerShell. The normal test suite retains its skip attributes to prevent unapproved database-changing execution.

## D-011 — SMTP Invitation Delivery Deferral
Status: Approved

Decision:
Defer live SMTP invitation delivery. Pastor-created account provisioning remains unavailable until SMTP is securely configured and invitation delivery is tested.

Reason:
No SMTP credentials or approved delivery provider are configured. Returning setup tokens to the browser or using an insecure local email workaround would weaken the account-provisioning security model.

Consequence:
The Authentication Core is ready for sign-off pending the final manual expired-session verification, while SMTP-dependent account provisioning remains pending. Required configuration includes secure SMTP host, port, sender, TLS setting, and secrets stored outside source control. The existing endpoint must continue to fail safely when SMTP is unavailable.

## D-012 — Development Pastor Password Recovery
Status: Approved

Decision:
Provide a Development-only, interactive maintenance operation to reset the single enabled, email-confirmed Pastor account’s password. It must preserve the approved roles and invalidate existing sessions.

Reason:
The initial local Pastor account requires a controlled recovery path without public self-service password reset or secret disclosure.

Consequence:
The operation uses strict local database targeting, password validation, confirmation, a database guard, and an Identity password-reset token. Three fixture-owned SQL Server integration tests passed locally. It is not a public endpoint and does not change the SMTP deferral.

## D-013 — Member Management Phase 1 Boundaries
Status: Approved

Decision:
Plan Member Management Phase 1 for approximately 20–25 boarding residents with Student and Worker profile types. Workers are external-company employees residing at JMH. Former residents are archived and archive access is Pastor-only. Approved profile photos are visible to authenticated users; Pastors manage all photos, authenticated residents manage their own photos, and under-18 photos require appropriate guardian permission.

Reason:
These rules establish the initial resident, archival, photo, and safeguarding boundaries before data-model design.

Consequence:
They are requirements and authorization constraints only. Detailed fields, database relationships, retention implementation, and permanent deletion remain proposed and require separate approval.

## D-014 — Member Management V1 Implementation and Photo Boundary
Status: Approved

Decision:
Implement Student and Worker profiles, DOB, type-specific details, ordered contacts, archive/restore and audit history using the existing Member Management schema. Store photo files in private server-side storage under random keys; store metadata and consent in SQL Server. Linked residents and Pastors may submit photos, Pastors alone approve or reject them, and authenticated users may retrieve only the approved current photo. Under-18 upload and approval require active consent tied to a guardian contact.

Reason:
This completes the approved D-013 boundary while keeping photo bytes outside public static storage and enforcing safeguarding and record-level authorization in the API.

Consequence:
No new migration was required for Fast-Track Sprint 1. Existing Member Management migrations still require separate review and approval before application to `JMHConnectDev`. Production must configure persistent private photo storage and backup/retention operations.

## D-015 — Member Self-Service and Dashboard Boundary
Status: Approved

Decision:
Linked Members may update their preferred name and contacts through an authenticated account-derived endpoint. Members may add or remove ordinary contacts and update contact communication details, but guardian identity/designation and guardian removal remain Pastor-only. Official identity, residency, education/work, consent, archive, audit and photo approval remain Pastor-only. Dashboard links only implemented routes; future modules are labelled upcoming.

Reason:
Provide useful self-service without enabling profile-ID targeting, mass assignment or changes that could undermine guardian consent and MM-14 privacy.

Consequence:
No database migration is required. Self-service changes are audited as `MemberSelfUpdated`; manual browser acceptance with the disposable dummy accounts remains required.

## D-016 — Devotion & Attendance V1 Boundary
Status: Approved

Decision:
All authenticated roles may view devotion schedules. Pastors alone create, edit and cancel sessions, manage attendance for active residents, view attendance summaries/rosters and inspect audit history. Attendance statuses are Present, Absent and Excused. A linked active Member may view only their own attendance history, resolved from the authenticated account. Cancelled sessions are immutable for edits and attendance.

Reason:
This delivers the approved Sprint 3 operational workflow while keeping roster details, management notes and audit records behind the Pastor boundary and preserving MM-14 record-level privacy.

Consequence:
The additive migration `20261030084013_AddDevotionAttendanceV1` introduces three tables with restrictive foreign keys, status/duration constraints and unique session/member attendance. It was applied only to the ownership-verified disposable acceptance database after explicit approval. Leader attendance-management permissions remain a future stakeholder decision.

## D-017 — Duties & Events V1 Boundary
Status: Approved

Decision:
Pastors create, edit, reassign and cancel Assigned duties for active residents and view duty audit history. A linked active Member sees only their own duties and may mark only their own Assigned duty Completed; completed/cancelled duties are immutable and Leaders receive no duty access in V1. All authenticated users may view community events, while Pastors alone create, edit, cancel and audit them.

Reason:
This delivers the explicitly requested Sprint 4 workflows while preserving account-derived ownership, active-resident constraints, least privilege and historical auditability.

Consequence:
Migration `20261030091728_AddDutiesAndEventsV1` adds duty, event and audit tables with restrictive foreign keys and schedule/status constraints. It requires separate approval before any database application. Future Leader permissions and retention/export rules remain stakeholder decisions.

## Open Decisions
- Final Member, Leader and Pastor permissions beyond the implemented boundaries.
- Production photo/database backup and retention policy.
- Deployment target, budget and backup policy.
- Consent and access rules for sensitive or minors' information beyond the implemented photo boundary.
- Final acceptance criteria for each V1 module.

## Updating This Log
When a decision changes:
1. Record the new decision and reason.
2. Update the status of the earlier decision.
3. Mark replaced decisions as Superseded where appropriate.
4. Keep the history rather than silently rewriting important decisions.
