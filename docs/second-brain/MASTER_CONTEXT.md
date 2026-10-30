# JMH Connect — Master Context

## 1. Project Purpose
JMH Connect is a digital management system for Jaffna Mission House (JMH), a spiritual boarding home.

The system aims to organize members, spiritual activities, attendance, duties, events and announcements while supporting the community's daily operations.

## 2. Project Status
Current phase: Sprint 6 Reports & Final Integration implemented and build-verified; no schema migration is required or applied, and production rollout is not approved.
Inspect the repository and test evidence before claiming an implementation is complete.

### 2026-10-30 Sprint 4 Checkpoint
- Pastors can create, edit and cancel scheduled duties for active residents and inspect duty audit history. Linked active Members receive only their own duties and can mark only their own assigned duties complete; Leaders receive no duty access in V1.
- Authenticated users can view community events in responsive list and monthly calendar views. Pastors alone create, edit, cancel and audit events.
- Dashboard and global navigation link to working Duties and Events modules. Birthdays and Announcements remain clearly labelled upcoming modules.
- Additive migration `20261030091728_AddDutiesAndEventsV1` creates duty, event and audit tables with restrictive foreign keys and schedule/status constraints. It was applied only to the ownership-verified disposable acceptance database by explicit approval.
- Backend Sprint4 build passed with zero warnings; 77 non-database tests passed. Angular production build passed and 33 tests passed. EF reports no model drift after the migration.

### 2026-10-30 Sprint 3 Checkpoint
- Pastors can schedule, edit and cancel devotion sessions, manage attendance for active residents only, view attendance summaries and inspect session audit history. Other authenticated roles can view the schedule but cannot mutate it or view management data.
- A linked Member can view only their own attendance history, resolved from the signed-in Identity account. Missing or archived links fail safely without exposing another resident. Attendance notes, rosters and audit records remain Pastor-only under MM-14.
- Devotion and attendance pages are responsive and role-aware. Dashboard and global navigation now link to the working Devotions module; the remaining unimplemented modules stay clearly labelled as upcoming.
- My Profile official-card values now render explicit `Not recorded` fallbacks and use accessible light text over the dark identity card.
- Additive migration `20261030084013_AddDevotionAttendanceV1` was generated, reviewed and later applied only to the ownership-verified disposable `JMHConnectAcceptance_20261030_01` database by explicit approval.
- Automated verification: backend non-database suite passed 73 tests; Angular passed 27 tests; backend and Angular production builds passed. EF reports no pending model changes after the migration.

### 2026-10-30 Sprint 2 Checkpoint
- Linked Members can retrieve and update their own active profile through an account-derived `/api/members/me` boundary. The API permits preferred name and owned contacts only; official identity/residency fields, guardian identity/designation, consent, archive and audit remain Pastor-controlled.
- Members may add and update ordinary contacts, update guardian phone/email and emergency status, but cannot rename or remove guardian contacts. Photo submission continues through the Sprint 1 ownership, consent and Pastor-approval workflow.
- `/home` is now a responsive role-aware dashboard with working My Profile and Family Directory navigation. Devotions, Duties, Events, Birthdays and Announcements are explicitly labelled as upcoming modules and have no fake feature routes.
- Backend Release build passed to an isolated output with zero warnings; 64 non-database backend tests passed. Angular production build passed and 19 tests passed. EF reports no model changes since the last migration.
- The approved disposable `JMHConnectAcceptance_20261030_01` database was verified read-only with its ownership marker, three migrations, one dummy Pastor, one linked dummy Member and two dummy profiles. Browser login/workflow acceptance remains pending because credentials were not supplied to this work session.

### 2026-10-30 Fast-Track Sprint 1 Checkpoint
- Member Management V1 now includes complete Student and Worker create/edit forms, date of birth, full type-specific fields, ordered family/emergency contacts, validation, archive/restore, and Pastor-only audit history.
- Secure profile photos use private server-side file storage with random keys. Only approved current photos are returned to authenticated users. A linked resident or Pastor may submit a photo; only a Pastor may approve or reject it.
- Residents under 18 require an active guardian-consent record tied to a guardian contact before photo upload and again at approval. Pastors can record and withdraw consent.
- MM-14 privacy is enforced by the API: directory summaries exclude sensitive fields, unrelated authenticated users receive public active-profile fields only, linked owners receive their own details, archive and audit access remain Pastor-only, and pending photos are private.
- Backend Release build passed with 0 warnings and 0 errors. The full normal backend suite passed 51, failed 0, skipped 10 database-changing SQL Server tests. Angular production build passed and 16 tests passed.
- The apparent backend test hang was caused by restricted sandbox testhost IPC. The same suite completed promptly on the unrestricted local host. A test-only reduced password-hash iteration count also keeps fixture setup fast.
- No migration was created or applied, `JMHConnectDev` was not changed, and no real account, commit or deployment action was performed.

### 2026-10-24 Checkpoint Correction
- Pastor password recovery is implemented as a Development-only, interactive maintenance operation. The password was recovered successfully, and the Pastor subsequently logged in with the replacement password.
- Three password-recovery SQL Server integration tests passed locally against fixture-owned disposable databases. They are still skipped in the normal suite because they create and delete temporary databases.
- Manual browser verification confirmed that `/home` displays the Pastor role and that direct `/home` navigation after logout redirects to `/login`.
- Manual expired-session handling remains to be verified. SMTP invitation delivery and SMTP-dependent account provisioning remain deferred and incomplete.
- Member Management Phase 1 has started as requirements and design work only. The confirmed scope is approximately 20–25 boarding residents; Student and Worker profile types; Workers as external-company employees residing at JMH; Pastor-only access to archived former-resident profiles; and approved profile photos visible to authenticated users. Pastors manage all photos, authenticated residents manage their own photos, and photos for residents under 18 require appropriate guardian permission.
- Permanent deletion, detailed Member Management fields, database relationships, and migrations remain proposed and unimplemented.

### 2026-10-13 Authentication Core Checkpoint
- Angular 21 frontend and ASP.NET Core 10 API authentication foundations are implemented.
- Authentication uses ASP.NET Core Identity cookie sessions, antiforgery protection, allowlisted credentialed development CORS, and backend authorization.
- Roles are exactly Pastor, Leader, and Member. Pastor administers login accounts; public self-registration is disabled.
- The initial Identity migration is applied to local `JMHConnectDev`; the first Pastor account was created and verified with the Pastor role.
- All six SQL Server integration tests passed individually from local Windows PowerShell. The normal backend suite preserves six skipped database-changing tests and currently reports 32 passed, 0 failed, 6 skipped. Angular currently reports 13 passed, 0 failed; both builds pass.
- Manual browser verification confirmed Pastor login, logout, logout-refresh behavior, `/home` role display, and direct-route redirect after logout. The protected Angular `/home` route and functional authentication guard are implemented; expired-session redirect remains a manual verification item.
- The Authentication Core is pending final manual expired-session verification. Live SMTP invitation delivery and Pastor-created account provisioning are deferred and incomplete until secure SMTP configuration and invitation testing are approved.

## 3. Technology Direction
- Frontend: Angular with TypeScript
- Backend: ASP.NET Core Web API using controllers
- Authentication: ASP.NET Core Identity
- ORM: Entity Framework Core
- Database: SQL Server
- Development environment: Windows
- Future hosting: Consider Azure only after checking eligibility, pricing and potential costs.

These are the intended technologies. Verify existing project files before changing or initializing anything.

## 4. Core V1 Features
- Authentication and authorization
- Member management and role management
- Dashboard
- Devotion sessions and attendance
- Duty assignments
- Event calendar
- Birthday reminders
- Announcements
- Basic reports

## 5. Roles
Proposed roles:
- Member
- Leader
- Pastor

Final permissions must be confirmed with JMH stakeholders. Never assume a role automatically has access to every record.

## 6. Official UI Palette
Use these four approved brand colors:
- Primary slate indigo: #464B71
- Secondary ocean blue: #118AB2
- Accent mint teal: #7CD5C7
- Background warm ivory: #F2F2ED

Design direction: classical elegance, premium UI, balanced spacing, refined borders and subtle shadows. Use the approved palette for color accents. Neutral surfaces and readable text colors are allowed.

## 7. Security and Privacy
- Enforce authorization in the backend API, not only in the frontend.
- Apply role-based and record-level authorization where required.
- Never commit passwords, API keys, connection-string secrets or other credentials.
- Never expose database credentials in Angular.
- Private journals must be owner-only by default if introduced later.
- Prayer requests must have explicit visibility and sharing controls if introduced later.
- Do not grant leaders or pastors automatic access to private content.
- Prefer deactivation over deleting members when history must be preserved.
- Store timestamps consistently in UTC and display times in Sri Lankan local time.
- Consider safeguarding and consent requirements before storing minors' personal information.

## 8. Scope Boundaries
Not planned for V1:
- AI spiritual adviser
- Public social network
- Live chat
- Payments
- Complex offline synchronization
- Native mobile applications
- Gamification

Possible later features include prayer requests, Bible reading plans, private journals, gallery, push notifications and Tamil localization.

## 9. Engineering Principles
- Inspect the existing repository before editing.
- Preserve existing architecture unless a change is justified.
- Prefer small, targeted and testable changes.
- Explain important decisions and their trade-offs.
- Never invent requirements when clarification is needed.
- Keep documentation synchronized with approved decisions.
- Do not mark a feature complete without verification.

## 10. Current Milestone
Sprint 6 Reports & Final Integration is build-verified. Reports use existing tables, so no migration is required.

## 11. Next Milestone
Run Pastor, Member and Leader browser acceptance with dummy data against `JMHConnectAcceptance_20261030_01`, including reports and in-app reminders. Never target `JMHConnectDev` without separate approval.

## 12. Source of Truth
Use the documents in this Second Brain as project guidance.
If documents conflict, identify the conflict instead of silently choosing.
Record confirmed decisions in DECISIONS_LOG.md.
Track progress in TASK_TRACKER.md.
Follow AGENT_RULES.md for AI-assisted work.
