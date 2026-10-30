# JMH Connect — Requirements Specification

## 1. Purpose
Define the initial functional and non-functional requirements for JMH Connect.

Requirements remain subject to stakeholder confirmation where explicitly marked as pending.

## 2. Functional Requirements

### FR-01: Authentication
- Users must authenticate before accessing protected features.
- Use the approved ASP.NET Core Identity approach.
- Handle invalid credentials and unauthorized requests safely.
- The approved Development-only Pastor password-recovery operation must remain interactive, protected by the existing database safeguards, and unavailable as public self-service recovery.
- SMTP-dependent account provisioning remains deferred until secure SMTP configuration and delivery testing are approved.

### FR-02: Member Management
- Authorized users can manage member records according to approved permissions.
- Validate required fields and prevent invalid or duplicate data.
- Preserve relevant historical records when a member is deactivated.
- Confirm which member details are visible to other users.
- Phase 1 supports approximately 20–25 boarding residents with Student and Worker profile types. Workers are external-company employees residing at JMH.
- Former residents are archived; archived profiles are visible only to Pastors.
- Approved profile photos are visible to authenticated users. Pastors manage all photos, and authenticated residents manage their own photos.
- Photos for residents under 18 require appropriate guardian permission.
- Permanent deletion remains out of scope; the implemented V1 fields are listed in DATABASE_DESIGN.md.

Implementation status (verified 2026-10-30): Student/Worker create and edit, DOB, type-specific details, ordered contacts, archive/restore, audit history, and MM-14 privacy are implemented. Profile photo submission, Pastor approval/rejection, linked-owner checks, and under-18 guardian consent are implemented. Permanent deletion remains out of scope.

Sprint 2 status (verified 2026-10-30): a linked Member can self-edit preferred name and permitted contact fields. Official profile fields remain Pastor-only. Guardian identity/designation and removal remain Pastor-only; Members may update guardian communication details without changing the consent subject. Self-service is resolved from the authenticated account and never accepts a target profile ID.

### FR-03: Roles and Authorization
- Initial proposed roles: Member, Leader and Pastor.
- Permissions must be explicitly defined and approved.
- Enforce authorization in the backend API.
- Check access to individual records where required.
- Frontend visibility alone is not an authorization mechanism.

### FR-04: Dashboard
- Show relevant information based on the authenticated user's permissions.
- Potential summaries include upcoming events, assigned duties and attendance.
- Confirm exact dashboard widgets before implementation.

Implementation status (verified 2026-10-30): the role-aware dashboard provides working My Profile, Family Directory, Devotions, Duties and Events navigation. Birthdays and Announcements remain clearly labelled upcoming modules.

### FR-05: Devotion Sessions
- Support organizing scheduled devotion sessions.
- Record relevant session details and dates.
- Confirm who can create, update, cancel and view sessions.

Sprint 3 status (verified 2026-10-30): authenticated users can view scheduled devotion sessions. Pastors alone can create, edit and cancel sessions and view session audit history. Duration is limited to 15–240 minutes, timestamps are stored in UTC, and cancelled sessions cannot be edited or receive attendance changes.

### FR-06: Attendance
- Record attendance for approved sessions or activities.
- Validate attendance entries and prevent unintended duplicates.
- Restrict attendance records according to approved permissions.
- Confirm attendance statuses and correction rules.

Sprint 3 status (verified 2026-10-30): Pastors can record or correct Present, Absent and Excused attendance for active residents only. The database prevents duplicate session/member rows. Pastors receive active-resident summaries and rosters; a linked active Member receives only their own history. Attendance notes, rosters and audit information are not exposed through Member endpoints.

### FR-07: Duty Assignments
- Support assigning responsibilities to members.
- Show users their relevant assigned duties.
- Confirm assignment, reassignment, completion and cancellation rules.

Sprint 4 status (verified 2026-10-30): Pastors create, edit, reassign and cancel duties while they remain Assigned, and may assign active residents only. A linked active Member receives only their own duty list and may mark only their own Assigned duty Completed. Completed/cancelled duties are immutable history, all mutations are audited, and Leaders receive no duty access in V1.

### FR-08: Events Calendar
- Display relevant upcoming events.
- Store event dates, times and basic details.
- Confirm who can create, edit and cancel events.
- Display times in Sri Lankan local time.

Sprint 4 status (verified 2026-10-30): authenticated users can view scheduled and cancelled community events in list and monthly calendar views. Pastors alone create, edit, cancel and view event audit history. Event end time must be later than its start, UTC is stored by the API and the Angular client displays local time.

### FR-09: Birthday Reminders
- Support birthday information and reminders where approved.
- Limit access to personal birthday data according to the approved privacy rules.
- Confirm reminder timing, recipients and delivery method.

Sprint 5 status (verified 2026-10-30): authenticated users receive only Pastor-approved active-resident display name and month/day/next occurrence. The shared response never includes full DOB, birth year or age. Visibility defaults private; only Pastors can change it and each change is audited.

### FR-10: Announcements
- Support publishing announcements to approved audiences.
- Restrict creation and editing to authorized users.
- Confirm whether announcements can target specific groups.

Sprint 5 status (verified 2026-10-30): Pastors create drafts, edit non-archived announcements, publish and archive them with audit history. Published messages are server-filtered for Everyone, Members, Leaders or Pastors; other roles never receive drafts or archived messages.

### FR-11: Basic Reports
- Provide useful summaries of approved operational data.
- Restrict report access according to user permissions.
- Confirm required reports, filters and export formats.

Sprint 6 status (build verified 2026-10-30): Pastor-only reports provide residents, devotion attendance, duties and events summaries/CSV exports with validated UTC ranges. Exports omit DOB, age, contact details and other MM-14 data; CSV cells are quoted and formula-neutralized.

## 3. Non-Functional Requirements

### Security
- Protect authenticated endpoints.
- Enforce backend authorization and input validation.
- Keep secrets out of source control and frontend code.
- Use secure configuration for database connections.
- Avoid exposing private information in logs or error responses.

### Privacy
- Collect only data required for approved features.
- Preserve private data according to explicit access rules.
- Obtain appropriate consent and safeguards before storing minors' information.
- Confirm retention and deletion policies.

### Usability
- Provide a clear and simple interface for non-technical users.
- Support desktop and mobile screen sizes.
- Use consistent navigation, validation and error messages.

### Maintainability
- Use Angular and TypeScript for the frontend.
- Use ASP.NET Core Web API with controllers for the backend.
- Use Entity Framework Core and SQL Server as the intended data stack.
- Verify the existing repository before choosing implementation details.

### Reliability and Testing
- Validate business rules on the server.
- Test authorization, validation and important workflows.
- Do not report tests as passing unless they have actually passed.

### Time Handling
- Store timestamps consistently in UTC where applicable.
- Display dates and times using the appropriate Sri Lankan local-time rules.
- Confirm date-only handling for birthdays and other calendar events.

## 4. Approved V1 Scope
- Authentication and authorization
- Member management
- Dashboard
- Devotion sessions
- Attendance
- Duty assignments
- Events calendar
- Birthday reminders
- Announcements
- Basic reports

## 5. Out of Scope for V1
- AI spiritual adviser
- Public social network
- Live chat
- Payments
- Complex offline synchronization
- Native mobile applications
- Gamification

## 6. Proposed Future Features
These are not approved V1 requirements:
- Prayer requests with explicit sharing controls
- Bible reading plans
- Private journals
- Gallery
- Push notifications
- Tamil localization

## 7. Pending Stakeholder Decisions
1. Exact permissions for Member, Leader and Pastor.
2. Member registration and account approval process.
3. Any fields or visibility rules proposed beyond the implemented Member Management V1 boundary.
4. Whether Leaders should receive future attendance-management permissions; V1 management is Pastor-only.
5. Whether Leaders should receive future duty-management permissions; V1 duty access is Pastor/linked-Member only.
6. Whether non-Pastor roles should receive future event-management permissions; V1 management is Pastor-only.
7. Birthday visibility and reminder recipients.
8. Announcement audience rules.
9. Required reports and export formats.
10. Data retention, backup and recovery expectations.
11. Notification channels.
12. Deployment, hosting and budget constraints.

## 8. Acceptance Principles
- A requirement is not complete merely because a screen exists.
- Business rules must be enforced where the data is processed.
- Authorization must be tested for allowed and denied access.
- Important workflows must be tested before being marked complete.
- Unconfirmed business rules must be recorded as pending rather than invented.
