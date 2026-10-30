# JMH Connect — Changelog

This file records meaningful project changes.
Do not record planned work as completed work.

## [Unreleased]

### Added
- 2026-10-30: Added Pastor-only operational reports for residents, devotion attendance, duties and events, with bounded date filters and safe CSV export.
- 2026-10-30: Added dashboard reminders for upcoming duties and events using existing data only; no scheduler or email delivery was introduced.
- 2026-10-30: Added Birthdays & Announcements V1: private-by-default Pastor-controlled birthday sharing, active-only family reminders, and Pastor-managed draft/publish/archive announcements with audience filtering and audit history.
- 2026-10-30: Added responsive Birthday and Announcement pages plus dashboard and global-navigation integration.
- 2026-10-30: Added Duties & Events V1 with Pastor-managed active-resident assignments, Member own-duty completion, shared community event list/calendar views and Pastor-only audit history.
- 2026-10-30: Added responsive Duties and Events pages plus working dashboard and global navigation.
- 2026-10-30: Added Devotion & Attendance V1: Pastor scheduling/cancellation, active-resident attendance management, summaries and audit history, plus linked-Member own-attendance history.
- 2026-10-30: Added responsive role-aware Devotions pages and working dashboard/navigation links.
- 2026-10-30: Added linked-Member self-profile editing for preferred name and permitted contacts, with account-derived ownership and field-level API enforcement.
- 2026-10-30: Replaced the basic welcome page with a responsive role-aware dashboard, working My Profile/Family Directory navigation and clearly labelled upcoming-module cards.
- 2026-10-30: Completed Member Management V1 Student/Worker forms, DOB, type-specific details, contacts, validation, archive/restore and Pastor audit history.
- 2026-10-30: Added private profile-photo storage, linked-owner/Pastor submission, Pastor approval/rejection, approved-photo delivery, and under-18 guardian-consent controls.
- Created the initial Second Brain documentation structure.
- Added MASTER_CONTEXT.md with project context and working principles.
- Added PROJECT_OVERVIEW.md with goals and proposed V1 scope.
- Added REQUIREMENTS.md with initial functional and non-functional requirements.
- Added ARCHITECTURE.md describing the intended application architecture.
- Added UI_DESIGN_SYSTEM.md with approved palette and interface guidelines.
- Added DATABASE_DESIGN.md with initial data-design principles.
- Added DECISIONS_LOG.md to track decisions and their status.
- Added TASK_TRACKER.md to track project planning and implementation work.
- Added AGENT_RULES.md with agent workflow and safety boundaries.

### Changed
- 2026-10-30: Applied `20261030095944_AddBirthdaysAndAnnouncementsV1` only to the ownership-verified disposable acceptance database; schema/data checks passed and no migrations remain pending.
- 2026-10-30: Generated and reviewed additive migration `20261030095944_AddBirthdaysAndAnnouncementsV1`; it has not been applied to any database.
- 2026-10-30: Generated and reviewed additive migration `20261030091728_AddDutiesAndEventsV1`; it was not applied to any database.
- 2026-10-30: Applied the previously approved Sprint 3 migration only to the ownership-verified disposable acceptance database; existing records were preserved.
- 2026-10-30: Generated and reviewed additive migration `20261030084013_AddDevotionAttendanceV1`; it was initially left unapplied and later applied only to the approved disposable acceptance database.
- 2026-10-30: Reconciled Second Brain status with the verified repository implementation. No migration was applied and no real account was changed.
- 2026-10-13: Implemented the Angular 21 and ASP.NET Core 10 authentication foundation using ASP.NET Core Identity cookie sessions, antiforgery protection, backend authorization, Pastor-only account creation, and account-setup email links.
- 2026-10-13: Added secure first-Pastor bootstrap hardening, strict CLI validation, and SQL Server integration-test fixture ownership safeguards.
- 2026-10-13: Added disabled-user session rejection and separate localhost-development cookie settings while preserving production Secure, HttpOnly, SameSite=Lax `__Host-` cookies.
- 2026-10-13: Completed the Authentication Core acceptance work: first-Pastor bootstrap, secure login/logout verification, backend authorization tests, and protected Angular `/home` navigation with a functional authentication guard.
- 2026-10-13: Formally deferred live SMTP invitation delivery. Pastor-created account provisioning remains unavailable until SMTP is securely configured and tested.
- 2026-10-24: Added and successfully used the Development-only Pastor password-recovery operation. The Pastor logged in with the replacement password after recovery.
- 2026-10-24: Began Member Management Phase 1 requirements and design. Confirmed resident categories, archival access, approved-photo access, and guardian-permission requirements; no Member Management database design or schema was implemented.

### Fixed
- 2026-10-30: Improved My Profile identity-card contrast and replaced blank official values with explicit `Not recorded` fallbacks.
- 2026-10-30: Diagnosed the apparent backend test hang as restricted sandbox testhost IPC; the full unrestricted suite completed promptly with 51 passed and 10 intentionally skipped. Added test-only faster password hashing for fixture setup.
- 2026-10-13: Corrected the SQL Server integration lock-contention assertion to match the guard message and added fixture cleanup locking and immediate ownership revalidation.

### Security
- 2026-10-30: Birthday DTOs exclude full DOB, birth year and age; archived residents are excluded. Announcement audience filtering and lifecycle enforcement occur in the backend.
- 2026-10-30: Enforced active-resident duty assignment, account-derived Member ownership completion, Leader duty denial and Pastor-only duty/event mutation and audit boundaries.
- 2026-10-30: Enforced Pastor-only devotion mutations, rosters and audit history; active-resident-only attendance; account-derived Member history; and MM-14 non-disclosure of other residents and management notes.
- 2026-10-30: Preserved MM-14, Pastor-only official fields, guardian identity/designation, consent, archive and audit boundaries during Member self-service. No schema change or migration was introduced.
- 2026-10-30: Verified MM-14 privacy for active summaries, unrelated authenticated viewers, linked owners, archived profiles, pending/approved photos and Pastor-only audit history.
- Documented the intended read-only MCP scope for docs/second-brain.
- Actual MCP configuration and read-only behavior remain unverified.
- 2026-10-13: Confirmed exactly three application roles: Pastor, Leader, and Member. No public registration endpoint or Admin role was introduced.
- 2026-10-13: SQL Server integration testing was attempted with a fixture-owned generated database only, but was blocked before database creation by the local SQL client encryption capability error. No application or disposable database was modified.
- 2026-10-13: The six SQL Server integration tests subsequently passed individually from local Windows PowerShell using fixture-owned disposable databases; cleanup was verified after each test.
- 2026-10-24: Three password-recovery SQL Server integration tests passed locally using fixture-owned disposable databases. Manual browser verification confirmed Pastor role display on `/home` and redirect to `/login` after logout.

## Changelog Rules
- Record only changes that have actually happened.
- Use clear, concise descriptions.
- Separate added, changed, fixed and security-related work where useful.
- Do not claim implementation or tests are complete without verification.
- Keep important history instead of silently removing earlier entries.
- Add a version or date when a meaningful release or milestone is established.
