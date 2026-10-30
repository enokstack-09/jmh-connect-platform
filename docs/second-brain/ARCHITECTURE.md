# JMH Connect — Architecture

## 1. Architecture Status
The Angular 21, ASP.NET Core 10, Identity, EF Core and SQL Server architecture is implemented. Authentication Core through Duties & Events V1 were locally build- and test-verified on 2026-10-30. Production deployment is not approved.

## 2. High-Level Structure

Angular Frontend
       |
       | HTTPS / JSON API
       v
ASP.NET Core Web API
       |
       +-- Authentication and Authorization
       +-- Business Logic
       +-- Input Validation
       +-- Entity Framework Core
                    |
                    v
                SQL Server

## 3. Frontend
Technology: Angular and TypeScript.

Responsibilities:
- Responsive user interface.
- Navigation and forms.
- Client-side input feedback.
- Display data returned by authorized API endpoints.
- Handle authentication state securely.

Rules:
- Never connect directly to SQL Server.
- Never store database credentials in frontend code.
- Do not rely on hidden UI elements for security.
- Follow UI_DESIGN_SYSTEM.md.

## 4. Backend
Technology: ASP.NET Core Web API using controllers.

Responsibilities:
- Expose API endpoints.
- Validate requests.
- Apply business rules.
- Authenticate users and authorize operations.
- Enforce record-level access where required.
- Return consistent error responses.
- Log relevant operational events without exposing secrets.

Prefer clear separation between controllers, application/business logic and data access. Follow existing repository conventions where appropriate.

## 5. Authentication and Authorization
Intended authentication technology: ASP.NET Core Identity.

Requirements:
- Protect private API endpoints.
- Apply explicit role and resource authorization.
- Validate access on the server for every protected operation.
- Confirm final role permissions with stakeholders.
- Keep authentication secrets and configuration out of source control.

Token, cookie, session and refresh-token strategies must be selected after checking the application type and deployment requirements.

## 6. Data Access
Intended database: SQL Server.
Intended ORM: Entity Framework Core.

Requirements:
- Use migrations or another controlled schema-management approach.
- Define primary keys, foreign keys, unique constraints and indexes where appropriate.
- Enforce critical business rules on the server and in the database where practical.
- Protect historical records when members are deactivated.
- Back up important data according to an approved policy.

Review generated migrations and obtain explicit approval before applying schema changes to a database containing JMH data.

Member profile photos use private server-side file storage with random keys. Files are never exposed as static content. Authorized API endpoints validate ownership, archive visibility, approval status, image signatures and size before storing or returning content. Metadata, consent and audit history remain in SQL Server.

Member self-service uses `/api/members/me`; the server resolves the profile exclusively from the authenticated Identity account link. A narrow request DTO prevents mass assignment of Pastor-managed fields. Contact ownership, guardian immutability, archive status and audit recording are enforced before persistence. This Sprint requires no schema change.

The Angular dashboard is the protected default route. Role-aware navigation exposes My Profile only to Members, and Family Directory plus Devotions to authenticated users. Remaining upcoming modules are informational cards rather than routes or simulated data.

Devotion management uses authenticated API reads and Pastor-only mutation, roster and audit endpoints. Attendance writes accept only unique active resident IDs and use a unique session/member database boundary. Member history resolves the resident from the authenticated Identity link and returns only that resident's session result; roster notes and audit details remain Pastor-only.

Duty management uses Pastor-only creation/edit/cancellation and active-assignee selection. Member list/completion resolves the resident exclusively from the authenticated Identity link and rejects cross-resident or archived ownership. Community events are authenticated shared information with Pastor-only mutation/audit endpoints. Both modules keep historical foreign keys restrictive and use UTC timestamps.

## 7. Suggested Code Organization
Use this as a guide, not a requirement to restructure an existing repository.

Frontend:
- Core services and authentication
- Shared UI components
- Feature areas
- Routing and authorization-aware navigation

Backend:
- Controllers / API endpoints
- Application services / business rules
- Data access and EF Core configuration
- Identity and authorization
- Request validation
- Tests

Avoid unnecessary abstraction and keep changes focused.

## 8. Security Boundaries
- Use HTTPS in deployed environments.
- Restrict CORS to known frontend origins.
- Validate input on the backend.
- Avoid mass assignment and unauthorized object access.
- Store secrets in suitable local configuration or secret-management systems.
- Do not expose private records through broad list endpoints.
- Apply least privilege to database and deployment identities.

## 9. Testing Strategy
- Unit tests for business rules.
- API integration tests for important workflows.
- Authorization tests for allowed and denied access.
- Validation tests for invalid input.
- Frontend tests for important user interactions.
- End-to-end tests for critical workflows when practical.

## 10. Deployment Direction
Develop locally first.
Evaluate hosting options only after requirements, budget, security and expected usage are known.
Check current eligibility and pricing before adopting paid cloud services.

## 11. Second Brain and Codex MCP
The intended MCP filesystem scope is limited to:
Z:\JMH\docs\second-brain

Security requirements:
- Mount only this directory into the MCP filesystem server.
- Enforce read-only access at the filesystem/container boundary.
- Do not mount the entire Z:\JMH project as the MCP filesystem root.
- Do not assume MCP read-only access restricts Codex's other tools.
- Keep source-code editing permissions separate from Second Brain permissions.
- Verify the actual mount is read-only before relying on it.

## 12. Open Architecture Decisions
- Existing repository and project structure.
- Authentication transport and session strategy.
- Backend project layering.
- Database migration workflow.
- Logging and backup approach.
- Deployment target and budget.
- CI/CD and automated testing setup.
