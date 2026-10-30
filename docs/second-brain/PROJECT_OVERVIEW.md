# JMH Connect — Project Overview

## 1. Product Summary
JMH Connect is a digital management system under active development for Jaffna Mission House (JMH), a spiritual boarding home.

It will help the community organize daily operations, members, spiritual activities, attendance, duties and events in one place.

Authentication Core through Reports & Final Integration are implemented and locally build-verified. Browser acceptance and production rollout remain pending.

## 2. Product Goals
- Reduce manual administrative work.
- Organize member information.
- Track attendance for approved activities.
- Coordinate duties and responsibilities.
- Manage events, announcements and birthdays.
- Provide useful summaries and basic reports.
- Offer a simple, responsive and accessible user experience.

## 3. Intended Users

### Member
Implemented capabilities:
- Maintain the linked profile’s preferred name and permitted contacts.
- Submit a profile photo through the consent and Pastor-approval workflow.
- Use the role-aware dashboard and authenticated family directory.
- View scheduled devotions and only their own linked attendance history.
- View and complete only their own linked duties, and browse community events.
- View Pastor-approved birthday reminders and announcements permitted for Members.

Future capabilities:
- View relevant announcements and events.
- View assigned duties.
- View permitted activity records.

### Leader
Potential capabilities:
- Help coordinate approved activities.
- Manage assigned duties or attendance where authorized.
- View relevant operational information.

### Pastor
Implemented capabilities:
- Access authorized management views.
- Schedule and cancel devotion sessions, manage active-resident attendance, and review summaries and audit history.
- Assign and track active-resident duties, and manage community events with audit history.

IMPORTANT:
These are proposed user categories and capabilities, not finalized permissions.
Confirm exact access rules with JMH stakeholders before implementation.

## 4. Core V1 Modules
1. Authentication and authorization
2. Member management
3. Dashboard
4. Devotion session management
5. Attendance tracking
6. Duty assignments
7. Event calendar
8. Birthday reminders
9. Announcements
10. Basic reports

## 5. Future Possibilities
Consider separately after V1:
- Prayer requests with explicit visibility controls
- Bible reading plans
- Private journals with owner-only access by default
- Photo gallery
- Push notifications
- Tamil localization

Do not treat these future features as approved V1 requirements.

## 6. V1 Non-Goals
- AI spiritual adviser
- Public social network
- Live chat
- Payment processing
- Complex offline synchronization
- Native Android or iOS applications
- Gamification

## 7. Design Direction
The interface should feel classical, elegant, calm and premium.

Official palette:
- Primary: #464B71
- Secondary: #118AB2
- Accent: #7CD5C7
- Background: #F2F2ED

The application must work well on desktop and mobile screens.

## 8. Quality Expectations
- Secure authentication and backend authorization.
- Clear validation and useful error messages.
- Responsive and accessible UI.
- Maintainable Angular and ASP.NET Core code.
- Reliable data relationships and validation.
- Appropriate automated tests.
- No secrets committed to source control.

## 9. Implementation Approach
1. Inspect the existing repository.
2. Confirm requirements and permissions.
3. Finalize architecture and database design.
4. Implement authentication and authorization.
5. Implement modules incrementally.
6. Test each module before proceeding.
7. Verify security, usability and integration.

## 10. Open Questions
- What are the final permissions for Member, Leader and Pastor?
- Which activities require attendance tracking?
- Who can create or edit duties and events?
- What reports are required?
- Which birthday information may be visible to other members?
- What notification channels are acceptable?

Do not invent answers to these questions. Record confirmed decisions separately.
