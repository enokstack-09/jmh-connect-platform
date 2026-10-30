# JMH Connect — Database Design

## 1. Status
The Member Management, Devotion, Duty, Event, Birthday and Announcement schemas are implemented through migrations applied only to the approved disposable acceptance database. Sprint 5 migration `20261030095944_AddBirthdaysAndAnnouncementsV1` was applied only to that database after ownership/history/data verification.

### Implemented Member Management Constraints
- Plan for approximately 20–25 boarding residents.
- Model Student and Worker resident types; Workers are external-company employees residing at JMH.
- Archive former residents and restrict archived-profile access to Pastors.
- Permit authenticated users to view approved profile photos. Pastors manage all photos; authenticated residents manage their own photos.
- Require appropriate guardian permission before using a photo of a resident under 18.
- Permanent deletion remains out of scope. Archive/restore preserves history.

### Implemented V1 Entities and Relationships
- `MemberProfiles`: names, home town, DOB, Student/Worker type, active/archive state, archive actor/time, timestamps and row version.
- `StudentResidentDetails` and `WorkerResidentDetails`: one-to-one type-specific profile details.
- `MemberContacts`: ordered family/emergency/guardian contacts with a unique profile/sort-order index.
- `MemberProfilePhotos`: private storage key and metadata, approval state, current-photo marker, creator/approver and row version.
- `PhotoGuardianConsents`: guardian contact, grant/withdraw state, evidence reference, notes and recording Pastor.
- `MemberProfileAuditEntries`: profile action, actor, UTC occurrence and correlation ID.
- `ApplicationUser.MemberProfileId`: optional unique link between one login account and one resident profile.

Delete behavior is restrictive for historical and safeguarding records. Photo bytes are stored outside the database in a private configured directory; the database stores only random storage keys and metadata. The API is the only supported read/write boundary.

### Implemented Devotion & Attendance Entities
- `DevotionSessions`: title, UTC start, 15–240 minute duration, location, notes, scheduled/cancelled status, creator, timestamps and row version.
- `DevotionAttendance`: one status per session/member (`Present`, `Absent` or `Excused`), optional Pastor note, recorder/time and row version. A unique `(DevotionSessionId, MemberProfileId)` index prevents duplicates.
- `DevotionSessionAuditEntries`: session action, actor, UTC occurrence and correlation ID.

Historical foreign keys use restrictive delete behavior. Attendance can be recorded only for active residents through the API. Member history is account-derived and does not expose rosters, other residents, management notes or audit history.

### Implemented Duties & Events Entities
- `DutyAssignments`: title/instructions, active-resident assignee, UTC schedule/deadline, Assigned/Completed/Cancelled status, completion actor/time, creator, timestamps and row version.
- `DutyAssignmentAuditEntries`: duty action, actor, UTC occurrence and correlation ID.
- `CommunityEvents`: title/description/location, UTC start/end, Scheduled/Cancelled status, creator, timestamps and row version.
- `CommunityEventAuditEntries`: event action, actor, UTC occurrence and correlation ID.

Database constraints enforce duty status/completion metadata, due time ordering, event status and end-after-start. Historical/user/member relationships use restrictive delete behavior. Member duty queries are account-derived; event and all audit mutations are authorized by the API.

### Implemented Birthdays & Announcements Entities
- `MemberProfiles.IsBirthdayVisible` defaults to false. Visibility actor/time are recorded with a restrictive Identity relationship and in the existing profile audit trail.
- Shared birthday queries select only active visible profiles with DOB and return display name, month/day and next occurrence; full DOB, year and age are never returned.
- `Announcements` stores lifecycle (Draft/Published/Archived), priority, audience, timestamps, creator and row version; `AnnouncementAuditEntries` stores action, actor, time and correlation ID.

Announcement lifecycle/enum values are constrained in SQL; API-only audience filtering and Pastor-only mutations enforce privacy and authorization.

## 2. Intended Technology
- Database: Microsoft SQL Server
- ORM: Entity Framework Core
- Schema changes: Controlled EF Core migrations or another explicitly approved workflow.

## 3. Design Principles
- Use clear, consistent table and column names.
- Define primary keys and foreign keys explicitly.
- Add unique constraints and indexes based on business rules and query patterns.
- Choose data types and nullability deliberately.
- Store timestamps consistently in UTC.
- Display dates and times in the user's appropriate local time zone.
- Use decimal types for exact numeric values where appropriate.
- Enforce important business rules on the server and, where practical, in the database.
- Avoid unnecessary duplication and overly complex relationships.
- Preserve historical records when a member is deactivated.

## 4. Security and Privacy
- Never expose database credentials in frontend code.
- Keep connection strings and secrets out of source control.
- Apply least-privilege database access.
- Protect personal and sensitive records through API authorization.
- Do not assume that hiding a frontend control protects a database record.
- Collect only information required for an approved feature.
- Confirm consent and safeguarding requirements before storing minors' information.
- Define explicit access rules before implementing private journals or prayer requests.

## 5. Data Integrity
- Define foreign-key behavior intentionally.
- Avoid cascade deletes where they could remove important historical records.
- Validate required fields and business constraints.
- Prevent duplicate records where the business rules require uniqueness.
- Use transactions for operations that must succeed or fail together.
- Consider concurrency when multiple authorized users can update the same record.

## 6. Proposed V1 Data Areas
These are planning categories, not approved table names:
- Users and authentication
- Member profiles
- Roles and authorization
- Devotion sessions
- Attendance
- Duty assignments
- Events
- Announcements
- Birthday-related information
- Reporting data, where required

Do not create every category as a separate table automatically.
Choose the schema based on confirmed requirements and relationships.

## 7. Entity Framework Core Rules
- Inspect the existing EF Core version and DbContext before changing them.
- Follow the repository's current conventions where reasonable.
- Keep entity configuration and relationships understandable.
- Review generated migrations before applying them.
- Never run destructive database commands without explicit approval.
- Do not reset, drop or recreate a database containing data without explicit approval.
- Explain migration risks before changing a shared or production database.

## 8. Required Design Documentation
Before implementing the schema, document:
- Entity purpose
- Columns and data types
- Primary keys and foreign keys
- Required and optional fields
- Unique constraints and indexes
- Relationships and delete behavior
- Business rules and validation
- Authorization and privacy implications
- Important queries and expected access patterns

## 9. Testing
Test important constraints, relationships, validation rules and authorization behavior.
Include migration review and database integration tests where practical.

## 10. Pending Decisions
- Final entity and table list
- User and member relationship
- Role and record-level access rules
- Any future duty/event retention or export policy
- Event and announcement visibility
- Birthday data visibility and consent
- Soft-delete or deactivation policy
- Audit retention duration and export requirements
- Backup and retention policy
