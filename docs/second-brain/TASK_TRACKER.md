# JMH Connect — Task Tracker

## Status Definitions
- Not Started
- In Progress
- Blocked
- In Review
- Done

## Current Phase
Sprint 6 Reports & Final Integration — implementation and build verification complete; browser acceptance is pending.

## 2026-10-30 Sprint 6

### Done
- [x] Add Pastor-only resident, devotion-attendance, duty and event summaries with bounded UTC date filtering.
- [x] Add formula-safe quoted CSV exports without DOB, age, contact or other MM-14 private fields.
- [x] Add dashboard duty/event reminders using existing data only; no email or background scheduler.
- [x] Add role-aware Reports navigation and report endpoint regression coverage.
- [x] Verify no schema/model change is needed and build API Release output with zero warnings; Angular production build passed.

### Pending / blocker
- [ ] Complete full backend/Angular test runs outside the restricted test-host IPC environment; both local runners were terminated before completion in this session.
- [ ] Run Pastor/Member/Leader browser acceptance against `JMHConnectAcceptance_20261030_01` with dummy data.

### Exact Next Action
- Start the latest API/frontend against `JMHConnectAcceptance_20261030_01`; verify role denial, report CSVs, dashboard reminders and existing Sprint 4/5 flows with dummy accounts only.

## 2026-10-30 Sprint 5

### Done
- [x] Add active-only, private-by-default birthday reminders/cards and dashboard integration without exposing DOB, year or age.
- [x] Add Pastor-only birthday visibility management with profile audit history.
- [x] Add Pastor-managed announcement draft/edit/publish/archive lifecycle, priority, audience filtering and audit history.
- [x] Add responsive Birthday/Announcement pages and working navigation for authenticated users.
- [x] Generate and review additive migration `20261030095944_AddBirthdaysAndAnnouncementsV1`; verify no EF model drift. Do not apply it.
- [x] Run automated verification: backend 81 passed/10 intentional SQL skips; Angular 33 passed; backend and Angular builds passed.

### Pending / blocker
- [x] Apply Sprint 5 migration only to `JMHConnectAcceptance_20261030_01` after ownership/history/data verification; verify new schema, preserved data and zero pending migrations.
- [ ] Run Pastor/Member/Leader browser acceptance with dummy data after the disposable schema is updated.

### Exact Next Action
- Run Pastor/Member/Leader browser acceptance against `JMHConnectAcceptance_20261030_01` with dummy data; never target `JMHConnectDev`.

## 2026-10-30 Sprint 4

### Done
- [x] Add Pastor-created/edited/cancelled duty assignments for active residents with UTC schedules and status tracking.
- [x] Add linked-Member own-duty listing and ownership-checked completion; reject archived, unlinked and cross-resident access.
- [x] Add authenticated community event list/monthly calendar with Pastor-only create, edit, cancel and audit history.
- [x] Add responsive Duties and Events pages plus role-aware dashboard/global navigation.
- [x] Generate and review additive migration `20261030091728_AddDutiesAndEventsV1`; verify four additive tables, twelve indexes, no destructive Up statements and no EF model drift. Do not apply it.
- [x] Run automated verification: backend 77 passed; Angular 33 passed; backend and Angular production builds passed.

### Pending / blocker
- [ ] Obtain explicit approval to apply the Sprint 4 migration to `JMHConnectAcceptance_20261030_01` only.
- [ ] Run Pastor/Member browser acceptance with dummy duties and events after that disposable schema is updated.

### Exact Next Action
- Review and approve the exact Sprint 4 migration target/procedure for `JMHConnectAcceptance_20261030_01`; never target `JMHConnectDev`.

## 2026-10-30 Sprint 3

### Done
- [x] Add authenticated devotion schedules with Pastor-only create, edit and cancel operations.
- [x] Add Pastor-managed Present/Absent/Excused attendance for active residents only, with duplicate prevention and summaries.
- [x] Add account-derived Member own-attendance history without roster, note or cross-resident disclosure.
- [x] Add Pastor-only devotion audit history and audited session/attendance mutations.
- [x] Add responsive role-aware Devotions pages, dashboard/navigation links and preserve upcoming labels for unimplemented modules.
- [x] Fix My Profile identity-card contrast and explicit missing-value display.
- [x] Generate and review additive migration `20261030084013_AddDevotionAttendanceV1`; verify no pending EF model changes. Do not apply it.
- [x] Run automated verification: backend non-database suite 73 passed; Angular 27 passed; backend and Angular production builds passed.

### Pending / blocker
- [x] Apply the Sprint 3 migration only to the ownership-verified disposable `JMHConnectAcceptance_20261030_01` after explicit approval.
- [ ] Run Pastor/Member browser acceptance with dummy data after that disposable schema is updated.

### Exact Next Action
- Run the remaining Pastor/Member Devotions browser acceptance with dummy sessions and attendance in `JMHConnectAcceptance_20261030_01`.

## 2026-10-30 Sprint 2

### Done
- [x] Add account-linked Member self-profile read/update endpoints with server-side ownership and field-level permission enforcement.
- [x] Permit preferred name and ordinary-contact management while preserving Pastor-only official fields, guardian identity/designation, consent, archive and audit access.
- [x] Reuse the secure photo ownership, guardian-consent and Pastor-approval workflow from Sprint 1.
- [x] Replace `/home` with a responsive role-aware dashboard and navigation.
- [x] Add working My Profile and Family Directory links; label Devotions, Duties, Events, Birthdays and Announcements as upcoming.
- [x] Verify no EF model drift. Backend Release build passed to an isolated output with 0 warnings; 64 backend tests and 19 Angular tests passed; Angular production build passed.
- [x] Verify only `JMHConnectAcceptance_20261030_01` read-only: correct ownership marker, 3 migrations, 1 dummy Pastor, 1 linked dummy Member and 2 dummy profiles.

### Pending / blocker
- [ ] Run manual Pastor and Member browser workflows. Dummy credentials were not available to this work session, so no login or data mutation was attempted.

### Exact Next Action
- Start API and Angular against `JMHConnectAcceptance_20261030_01`, then use the existing dummy Pastor and Member credentials to verify dashboard navigation, Member self-edit restrictions, photo submission/approval and MM-14 views.

## 2026-10-30 Fast-Track Sprint 1

### Done
- [x] Complete Student and Worker create/edit forms with DOB, type-specific details, contacts and client/server validation.
- [x] Implement private profile-photo storage, linked-owner/Pastor submission, Pastor approval/rejection, approved-photo viewing and safe replacement behavior.
- [x] Enforce active guardian consent for under-18 photo upload and approval; add Pastor grant/withdraw controls.
- [x] Add Pastor-only audit-history UI.
- [x] Complete and verify archive/restore flows and MM-14 privacy boundaries.
- [x] Polish the responsive family-directory UI using the approved palette.
- [x] Diagnose the backend test-host stall as restricted sandbox IPC and verify the suite on the unrestricted host.
- [x] Run verification: backend Release build passed; 51 backend tests passed and 10 database-changing SQL tests skipped; Angular production build passed; 16 Angular tests passed.
- [x] Preserve existing authentication and data: no migrations applied, no real accounts changed, no commit and no deployment.

### Pending
- [ ] Complete manual browser acceptance with dummy data against an approved disposable database.
- [ ] Review and approve the already-generated Member Management migrations before applying them to `JMHConnectDev`.
- [ ] Manually verify expired-session redirect behavior.
- [ ] Configure and test secure SMTP delivery before enabling live account provisioning.

### Exact Next Action
- Run the Member Management browser acceptance checklist with dummy Student and Worker records in an approved disposable database; do not target `JMHConnectDev` without separate approval.

## 2026-10-24 Checkpoint Correction

### Verified since the prior checkpoint
- [x] Implement and execute the Development-only Pastor password-recovery operation for the existing first Pastor account.
- [x] Run the three approved password-recovery SQL Server integration tests locally; all passed using fixture-owned disposable databases.
- [x] Verify that the Pastor can log in with the replacement password.
- [x] Verify that `/home` displays the signed-in Pastor role.
- [x] Verify that, after logout, direct navigation to `/home` redirects to `/login`.

### Member Management Phase 1 — confirmed requirements
- [x] Plan for approximately 20–25 boarding residents.
- [x] Support Student and Worker resident profile types; Workers are external-company employees residing at JMH.
- [x] Archive former residents; archived profiles are Pastor-only.
- [x] Make approved profile photos available to authenticated users. Pastors manage all photos; authenticated residents manage their own photos.
- [x] Require appropriate guardian permission for an under-18 resident’s photo.

### Pending
- [ ] Manually verify that an expired session redirects protected `/home` access to `/login`.
- [ ] Configure and test secure SMTP delivery before enabling Pastor-created account provisioning. Live invitation delivery is deferred; do not return setup tokens to browsers or use an insecure email workaround.
- [ ] Design, review, and separately approve the Member Management database model, including detailed fields, retention, and deletion behavior. Permanent deletion is proposed only.

### Next Action
- Historical 2026-10-24 action, superseded by the 2026-10-30 Fast-Track Sprint 1 checkpoint above.

## 2026-10-13 Authentication Checkpoint

### Done
- [x] Implement the Identity authentication foundation with the approved Pastor, Leader, and Member roles.
- [x] Apply and verify the approved initial Identity migration in `JMHConnectDev` before this checkpoint.
- [x] Add bootstrap safeguards and non-database authorization, authentication, Angular, and fixture-safety tests.
- [x] Run backend build and non-database tests: 27 passed, 0 failed, 6 SQL Server integration tests skipped.
- [x] Run Angular production build and automated tests: 2 passed, 0 failed.
- [x] Run all six SQL Server integration tests individually from local Windows PowerShell; each passed with fixture cleanup verified.
- [x] Create and verify the first Pastor account in `JMHConnectDev`; the account has the Pastor role and the approved three roles exist.
- [x] Complete Stage D automated authentication verification: backend 32 passed, 0 failed, 6 SQL integration tests skipped in the normal suite; Angular 13 passed, 0 failed.
- [x] Add the protected Angular `/home` route, functional authentication guard, login navigation, logout navigation, and role display.

### Pending
- [ ] Configure and test secure SMTP delivery before enabling Pastor-created account provisioning. Live invitation delivery is formally deferred; do not return setup tokens to browsers or use an insecure email workaround.
- [ ] Complete the remaining manual browser verification of protected-route handling after an expired session.

### Next Action
- Design and implement Member Management without changing the approved authentication and authorization boundaries.

## Completed
- [x] Create the Second Brain folder.
- [x] Create MASTER_CONTEXT.md.
- [x] Create PROJECT_OVERVIEW.md.
- [x] Create REQUIREMENTS.md.
- [x] Create ARCHITECTURE.md.
- [x] Create UI_DESIGN_SYSTEM.md.
- [x] Create DATABASE_DESIGN.md.
- [x] Create DECISIONS_LOG.md.
- [x] Create AGENT_RULES.md.

Note: These items confirm documentation creation only. They do not mean the application has been implemented or tested.

## In Progress
- [ ] Complete the initial planning documentation.
- [ ] Verify all document contents and consistency.
- [ ] Confirm the Codex application/interface being used.
- [ ] Configure and verify read-only MCP access to the Second Brain directory.

## Not Started
- [ ] Complete this task tracker.
- [ ] Complete CHANGELOG.md.
- [ ] Inspect the existing repository structure.
- [ ] Review requirements and obtain stakeholder confirmation.
- [ ] Finalize architecture and authentication decisions.
- [ ] Review and approve the database design.
- [ ] Define implementation milestones and acceptance criteria.
- [ ] Begin implementation after the required decisions are approved.

## Blockers and Risks
- Docker Desktop is not installed yet.
- MCP configuration has not been verified.
- Read-only access must be tested, not merely assumed.
- MCP restrictions do not automatically restrict other Codex tools.
- Final role permissions and some business rules remain undecided.
- Hosting and cloud costs have not been evaluated.
- SMTP-dependent account provisioning remains unavailable until secure SMTP configuration and invitation testing are approved.

## Working Rules
- Keep tasks small and independently verifiable.
- Update task status only when supported by actual results.
- Record blockers and explain how to resolve them.
- Do not mark code, migrations or tests as complete unless they have actually been checked.
- Protect existing project files and data.

## Historical Next Action
Finish the planning documentation, verify the files, then configure and test the intended read-only MCP connection.
