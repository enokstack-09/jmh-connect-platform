using System.ComponentModel.DataAnnotations;
using JMHConnect.Api.Data;
using JMHConnect.Api.Models;
using JMHConnect.Api.Security;
using JMHConnect.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JMHConnect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/members")]
public sealed class MembersController(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IProfilePhotoStorage photoStorage) : ControllerBase
{
    private const long MaximumPhotoBytes = 5 * 1024 * 1024;
    private static readonly TimeZoneInfo SriLankaTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "Sri Lanka Standard Time" : "Asia/Colombo");
    // MM-14 privacy boundary: authenticated users receive only active, non-sensitive summaries.
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MemberSummaryResponse>>> List(
        [FromQuery] string? residentType,
        [FromQuery, StringLength(100)] string? search,
        CancellationToken cancellationToken)
    {
        if (!TryParseResidentType(residentType, out var type))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(residentType)] = ["Resident type must be Student or Worker."]
            }));
        }

        var query = dbContext.MemberProfiles.AsNoTracking()
            .Where(profile => profile.ResidencyStatus == ResidencyStatus.Active);
        if (type is not null)
        {
            query = query.Where(profile => profile.ResidentType == type);
        }

        var normalizedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(profile =>
                (profile.GivenName ?? string.Empty).Contains(normalizedSearch) ||
                (profile.FamilyName ?? string.Empty).Contains(normalizedSearch) ||
                (profile.PreferredName ?? string.Empty).Contains(normalizedSearch));
        }

        var profiles = await query
            .OrderBy(profile => profile.FamilyName)
            .ThenBy(profile => profile.GivenName)
            .Select(profile => new MemberSummaryResponse(
                profile.Id,
                profile.GivenName ?? string.Empty,
                profile.FamilyName ?? string.Empty,
                profile.PreferredName,
                profile.HomeTown,
                profile.ResidentType,
                profile.Photos.Any(photo => photo.IsCurrent && photo.ApprovalStatus == PhotoApprovalStatus.Approved)))
            .ToListAsync(cancellationToken);
        return Ok(profiles);
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpGet("archived")]
    public async Task<ActionResult<IReadOnlyList<MemberArchivedSummaryResponse>>> ListArchived(CancellationToken cancellationToken)
    {
        var profiles = await dbContext.MemberProfiles.AsNoTracking()
            .Where(profile => profile.ResidencyStatus == ResidencyStatus.Archived)
            .OrderByDescending(profile => profile.ArchivedUtc)
            .ThenBy(profile => profile.FamilyName)
            .ThenBy(profile => profile.GivenName)
            .Select(profile => new MemberArchivedSummaryResponse(
                profile.Id,
                profile.GivenName ?? string.Empty,
                profile.FamilyName ?? string.Empty,
                profile.PreferredName,
                profile.HomeTown,
                profile.ResidentType,
                profile.ArchivedUtc,
                profile.Photos.Any(photo => photo.IsCurrent && photo.ApprovalStatus == PhotoApprovalStatus.Approved)))
            .ToListAsync(cancellationToken);
        return Ok(profiles);
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpGet("archived/{id:int}")]
    public async Task<ActionResult<MemberDetailsResponse>> GetArchived(int id, CancellationToken cancellationToken)
    {
        var profile = await LoadProfileAsync(id, cancellationToken);
        if (profile is null || profile.ResidencyStatus != ResidencyStatus.Archived) return NotFound();
        return Ok(ToDetails(profile, true));
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpGet("{id:int}/history")]
    public async Task<ActionResult<IReadOnlyList<MemberProfileHistoryResponse>>> GetHistory(int id, CancellationToken cancellationToken)
    {
        var profileExists = await dbContext.MemberProfiles.AsNoTracking().AnyAsync(profile => profile.Id == id, cancellationToken);
        if (!profileExists) return NotFound();

        var history = await dbContext.MemberProfileAuditEntries.AsNoTracking()
            .Where(entry => entry.MemberProfileId == id)
            .OrderByDescending(entry => entry.OccurredUtc)
            .ThenByDescending(entry => entry.Id)
            .Select(entry => new MemberProfileHistoryResponse(entry.Id, entry.ActionType, entry.OccurredUtc,
                entry.ActorUser == null ? "Unknown account" : entry.ActorUser.Email ?? "Unknown account"))
            .ToListAsync(cancellationToken);
        return Ok(history);
    }

    [HttpGet("me")]
    public async Task<ActionResult<MemberDetailsResponse>> GetMyProfile(CancellationToken cancellationToken)
    {
        var profile = await LoadLinkedProfileAsync(cancellationToken);
        if (profile is null) return NotFound(new { message = "No member profile is linked to this account." });
        if (profile.ResidencyStatus != ResidencyStatus.Active)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Your linked member profile is archived. Ask a Pastor to restore it before using My Profile." });
        return Ok(ToDetails(profile, true));
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyProfile(
        SelfMemberUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var profile = await LoadLinkedProfileAsync(cancellationToken);
        if (profile is null) return NotFound(new { message = "No member profile is linked to this account." });
        if (profile.ResidencyStatus != ResidencyStatus.Active)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Your linked member profile is archived. Ask a Pastor to restore it before using My Profile." });

        if (!TryValidateSelfContacts(request.Contacts, out var validationErrors))
        {
            return BadRequest(new ValidationProblemDetails(validationErrors));
        }

        var now = DateTimeOffset.UtcNow;
        profile.PreferredName = TrimToNull(request.PreferredName);
        if (!TryApplySelfContactChanges(profile, request.Contacts, now, out validationErrors))
        {
            return BadRequest(new ValidationProblemDetails(validationErrors));
        }

        profile.UpdatedUtc = now;
        AddAuditEntry(profile, userManager.GetUserId(User)!, "MemberSelfUpdated", now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:int}/photo")]
    public async Task<IActionResult> GetApprovedPhoto(int id, CancellationToken cancellationToken)
    {
        var profile = await dbContext.MemberProfiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (profile is null) return NotFound();
        if (profile.ResidencyStatus == ResidencyStatus.Archived && !User.IsInRole(ApplicationRoles.Pastor)) return Forbid();

        var photo = await dbContext.MemberProfilePhotos.AsNoTracking()
            .Where(item => item.MemberProfileId == id && item.IsCurrent && item.ApprovalStatus == PhotoApprovalStatus.Approved)
            .SingleOrDefaultAsync(cancellationToken);
        if (photo is null) return NotFound();

        var stream = await photoStorage.OpenReadAsync(photo.StorageKey, cancellationToken);
        return stream is null ? NotFound() : File(stream, photo.ContentType, enableRangeProcessing: false);
    }

    [HttpGet("{id:int}/photo-management")]
    public async Task<ActionResult<PhotoManagementResponse>> GetPhotoManagement(int id, CancellationToken cancellationToken)
    {
        var profile = await dbContext.MemberProfiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (profile is null) return NotFound();
        if (!await CanManagePhotoAsync(profile, cancellationToken)) return Forbid();

        var photos = await dbContext.MemberProfilePhotos.AsNoTracking()
            .Where(item => item.MemberProfileId == id)
            .OrderByDescending(item => item.CreatedUtc)
            .Select(item => new PhotoStateResponse(item.Id, item.ApprovalStatus, item.CreatedUtc, item.IsCurrent))
            .ToListAsync(cancellationToken);
        var consent = await dbContext.PhotoGuardianConsents.AsNoTracking()
            .Where(item => item.MemberProfileId == id && item.ConsentStatus == PhotoConsentStatus.Granted && item.WithdrawnUtc == null)
            .Select(item => new PhotoConsentResponse(item.Id, item.GuardianContactId, item.GrantedUtc, item.EvidenceReference, item.Notes))
            .SingleOrDefaultAsync(cancellationToken);

        return Ok(new PhotoManagementResponse(
            RequiresGuardianConsent(profile.DateOfBirth),
            consent,
            photos.FirstOrDefault(item => item.ApprovalStatus == PhotoApprovalStatus.Pending),
            photos.FirstOrDefault(item => item.ApprovalStatus == PhotoApprovalStatus.Approved && item.IsCurrent)));
    }

    [RequestSizeLimit(MaximumPhotoBytes + 64 * 1024)]
    [HttpPost("{id:int}/photo")]
    public async Task<ActionResult<PhotoStateResponse>> UploadPhoto(
        int id,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.MemberProfiles.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (profile is null) return NotFound();
        if (profile.ResidencyStatus == ResidencyStatus.Archived)
            return Conflict(new { message = "Archived profiles cannot receive a new photo." });
        if (!await CanManagePhotoAsync(profile, cancellationToken)) return Forbid();
        if (profile.DateOfBirth is null)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["dateOfBirth"] = ["Date of birth is required before adding a photo."] }));
        if (RequiresGuardianConsent(profile.DateOfBirth) && !await HasActiveGuardianConsentAsync(id, cancellationToken))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["guardianConsent"] = ["Active guardian consent is required for a resident under 18."] }));
        if (await dbContext.MemberProfilePhotos.AnyAsync(
                item => item.MemberProfileId == id && item.ApprovalStatus == PhotoApprovalStatus.Pending,
                cancellationToken))
            return Conflict(new { message = "A profile photo is already awaiting Pastor review." });
        if (file is null || file.Length == 0 || file.Length > MaximumPhotoBytes)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["file"] = [$"Choose an image up to {MaximumPhotoBytes / 1024 / 1024} MB."] }));

        await using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, cancellationToken);
        if (!TryIdentifyImage(buffer.GetBuffer().AsSpan(0, (int)buffer.Length), out var contentType, out var extension))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["file"] = ["Only valid JPEG, PNG or WebP images are accepted."] }));
        buffer.Position = 0;

        var storageKey = await photoStorage.SaveAsync(buffer, extension, cancellationToken);
        try
        {
            var actorId = userManager.GetUserId(User)!;
            var now = DateTimeOffset.UtcNow;
            var photo = new MemberProfilePhoto
            {
                MemberProfileId = id,
                StorageKey = storageKey,
                OriginalFileName = Path.GetFileName(file.FileName) is { Length: > 255 } longName ? longName[..255] : Path.GetFileName(file.FileName),
                ContentType = contentType,
                FileSizeBytes = file.Length,
                ApprovalStatus = PhotoApprovalStatus.Pending,
                IsCurrent = false,
                CreatedUtc = now,
                CreatedByUserId = actorId
            };
            dbContext.MemberProfilePhotos.Add(photo);
            AddAuditEntry(profile, actorId, "PhotoSubmitted", now);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Accepted(new PhotoStateResponse(photo.Id, photo.ApprovalStatus, photo.CreatedUtc, photo.IsCurrent));
        }
        catch
        {
            await photoStorage.DeleteIfExistsAsync(storageKey, cancellationToken);
            throw;
        }
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpPost("{id:int}/photo-consent")]
    public async Task<ActionResult<PhotoConsentResponse>> GrantPhotoConsent(
        int id,
        GrantPhotoConsentRequest request,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.MemberProfiles.Include(item => item.Contacts)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (profile is null) return NotFound();
        if (profile.ResidencyStatus == ResidencyStatus.Archived)
            return Conflict(new { message = "Archived profiles cannot receive new consent records." });
        if (profile.DateOfBirth is null)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["dateOfBirth"] = ["Date of birth is required before recording photo consent."] }));
        if (!RequiresGuardianConsent(profile.DateOfBirth))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["dateOfBirth"] = ["Guardian photo consent is recorded only for residents under 18."] }));
        if (!profile.Contacts.Any(contact => contact.Id == request.GuardianContactId && contact.IsGuardian))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(request.GuardianContactId)] = ["Select a guardian contact belonging to this resident."] }));
        if (await HasActiveGuardianConsentAsync(id, cancellationToken))
            return Conflict(new { message = "Active guardian photo consent already exists." });

        var actorId = userManager.GetUserId(User)!;
        var now = DateTimeOffset.UtcNow;
        var consent = new PhotoGuardianConsent
        {
            MemberProfileId = id,
            GuardianContactId = request.GuardianContactId,
            GrantedUtc = now,
            ConsentStatus = PhotoConsentStatus.Granted,
            RecordedByUserId = actorId,
            EvidenceReference = TrimToNull(request.EvidenceReference),
            Notes = TrimToNull(request.Notes)
        };
        dbContext.PhotoGuardianConsents.Add(consent);
        AddAuditEntry(profile, actorId, "PhotoConsentGranted", now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new PhotoConsentResponse(consent.Id, consent.GuardianContactId, consent.GrantedUtc, consent.EvidenceReference, consent.Notes));
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpPost("{id:int}/photo-consent/withdraw")]
    public async Task<IActionResult> WithdrawPhotoConsent(int id, CancellationToken cancellationToken)
    {
        var profile = await dbContext.MemberProfiles.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (profile is null) return NotFound();
        var consent = await dbContext.PhotoGuardianConsents.SingleOrDefaultAsync(
            item => item.MemberProfileId == id && item.ConsentStatus == PhotoConsentStatus.Granted && item.WithdrawnUtc == null,
            cancellationToken);
        if (consent is null) return Conflict(new { message = "No active guardian photo consent exists." });

        var now = DateTimeOffset.UtcNow;
        consent.ConsentStatus = PhotoConsentStatus.Withdrawn;
        consent.WithdrawnUtc = now;
        var currentPhoto = await dbContext.MemberProfilePhotos.SingleOrDefaultAsync(
            item => item.MemberProfileId == id && item.IsCurrent, cancellationToken);
        if (currentPhoto is not null) currentPhoto.IsCurrent = false;
        AddAuditEntry(profile, userManager.GetUserId(User)!, "PhotoConsentWithdrawn", now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpPost("{id:int}/photos/{photoId:int}/approve")]
    public async Task<IActionResult> ApprovePhoto(int id, int photoId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.MemberProfiles.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (profile is null) return NotFound();
        var photo = await dbContext.MemberProfilePhotos.SingleOrDefaultAsync(
            item => item.Id == photoId && item.MemberProfileId == id, cancellationToken);
        if (photo is null) return NotFound();
        if (photo.ApprovalStatus != PhotoApprovalStatus.Pending)
            return Conflict(new { message = "Only pending photos can be approved." });
        await using var photoContent = await photoStorage.OpenReadAsync(photo.StorageKey, cancellationToken);
        if (photoContent is null) return Conflict(new { message = "The submitted photo file is unavailable and cannot be approved." });
        if (RequiresGuardianConsent(profile.DateOfBirth) && !await HasActiveGuardianConsentAsync(id, cancellationToken))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["guardianConsent"] = ["Active guardian consent is required before approval."] }));

        var current = await dbContext.MemberProfilePhotos.SingleOrDefaultAsync(
            item => item.MemberProfileId == id && item.IsCurrent, cancellationToken);
        if (current is not null) current.IsCurrent = false;
        var now = DateTimeOffset.UtcNow;
        var actorId = userManager.GetUserId(User)!;
        photo.ApprovalStatus = PhotoApprovalStatus.Approved;
        photo.ApprovedUtc = now;
        photo.ApprovedByUserId = actorId;
        photo.IsCurrent = true;
        AddAuditEntry(profile, actorId, "PhotoApproved", now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpPost("{id:int}/photos/{photoId:int}/reject")]
    public async Task<IActionResult> RejectPhoto(int id, int photoId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.MemberProfiles.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (profile is null) return NotFound();
        var photo = await dbContext.MemberProfilePhotos.SingleOrDefaultAsync(
            item => item.Id == photoId && item.MemberProfileId == id, cancellationToken);
        if (photo is null) return NotFound();
        if (photo.ApprovalStatus != PhotoApprovalStatus.Pending)
            return Conflict(new { message = "Only pending photos can be rejected." });

        photo.ApprovalStatus = PhotoApprovalStatus.Rejected;
        photo.IsCurrent = false;
        AddAuditEntry(profile, userManager.GetUserId(User)!, "PhotoRejected", DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        await photoStorage.DeleteIfExistsAsync(photo.StorageKey, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<object>> Get(int id, CancellationToken cancellationToken)
    {
        var profile = await LoadProfileAsync(id, cancellationToken);
        if (profile is null) return NotFound();

        var isPastor = User.IsInRole(ApplicationRoles.Pastor);
        if (profile.ResidencyStatus == ResidencyStatus.Archived && !isPastor)
        {
            return Forbid();
        }

        if (isPastor)
        {
            return Ok(ToDetails(profile, true));
        }

        var currentUserId = userManager.GetUserId(User);
        var isLinkedOwner = !string.IsNullOrWhiteSpace(currentUserId) && await dbContext.Users.AsNoTracking()
            .AnyAsync(user => user.Id == currentUserId && user.MemberProfileId == profile.Id, cancellationToken);
        return isLinkedOwner ? Ok(ToDetails(profile, true)) : Ok(ToPublicProfile(profile));
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpPost]
    public async Task<ActionResult<MemberDetailsResponse>> Create(
        CreateMemberRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryValidateRequest(request, null, out var residentType, out var validationErrors))
        {
            return BadRequest(new ValidationProblemDetails(validationErrors));
        }

        var actorId = userManager.GetUserId(User)!;
        var now = DateTimeOffset.UtcNow;
        var profile = new MemberProfile
        {
            GivenName = request.GivenName.Trim(),
            FamilyName = request.FamilyName.Trim(),
            PreferredName = TrimToNull(request.PreferredName),
            HomeTown = request.HomeTown.Trim(),
            DateOfBirth = request.DateOfBirth,
            ResidentType = residentType,
            ResidencyStatus = ResidencyStatus.Active,
            CreatedUtc = now,
            UpdatedUtc = now
        };
        ApplyDetails(profile, request, residentType!.Value, now);
        AddNewContacts(profile, request.Contacts, now);
        AddAuditEntry(profile, actorId, "MemberCreated", now);
        dbContext.MemberProfiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = profile.Id }, ToDetails(profile, true));
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateMemberRequest request, CancellationToken cancellationToken)
    {
        var profile = await LoadProfileAsync(id, cancellationToken);
        if (profile is null) return NotFound();
        if (profile.ResidencyStatus == ResidencyStatus.Archived)
        {
            return Conflict(new { message = "Archived profiles cannot be edited. Restore is not part of this phase." });
        }

        if (!TryValidateRequest(request, profile, out var residentType, out var validationErrors))
        {
            return BadRequest(new ValidationProblemDetails(validationErrors));
        }

        var now = DateTimeOffset.UtcNow;
        profile.GivenName = request.GivenName.Trim();
        profile.FamilyName = request.FamilyName.Trim();
        profile.PreferredName = TrimToNull(request.PreferredName);
        profile.HomeTown = request.HomeTown.Trim();
        profile.DateOfBirth = request.DateOfBirth;
        profile.UpdatedUtc = now;
        ApplyDetails(profile, request, residentType!.Value, now);
        if (request.Contacts is not null && !TryApplyContactChanges(profile, request.Contacts, now, out validationErrors))
        {
            return BadRequest(new ValidationProblemDetails(validationErrors));
        }

        AddAuditEntry(profile, userManager.GetUserId(User)!, "MemberUpdated", now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpPost("{id:int}/archive")]
    public async Task<IActionResult> Archive(int id, CancellationToken cancellationToken)
    {
        var profile = await dbContext.MemberProfiles.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (profile is null) return NotFound();
        if (profile.ResidencyStatus == ResidencyStatus.Archived)
        {
            return Conflict(new { message = "The member profile is already archived." });
        }

        var now = DateTimeOffset.UtcNow;
        var actorId = userManager.GetUserId(User)!;
        profile.ResidencyStatus = ResidencyStatus.Archived;
        profile.ArchivedUtc = now;
        profile.ArchivedByUserId = actorId;
        profile.UpdatedUtc = now;
        AddAuditEntry(profile, actorId, "MemberArchived", now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "PastorOnly")]
    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id, CancellationToken cancellationToken)
    {
        var profile = await dbContext.MemberProfiles.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (profile is null) return NotFound();
        if (profile.ResidencyStatus != ResidencyStatus.Archived)
        {
            return Conflict(new { message = "The member profile is already active." });
        }

        var now = DateTimeOffset.UtcNow;
        var actorId = userManager.GetUserId(User)!;
        profile.ResidencyStatus = ResidencyStatus.Active;
        profile.ArchivedUtc = null;
        profile.ArchivedByUserId = null;
        profile.UpdatedUtc = now;
        AddAuditEntry(profile, actorId, "MemberRestored", now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<MemberProfile?> LoadProfileAsync(int id, CancellationToken cancellationToken) =>
        await dbContext.MemberProfiles
            .Include(profile => profile.StudentDetails)
            .Include(profile => profile.WorkerDetails)
            .Include(profile => profile.Contacts.OrderBy(contact => contact.SortOrder))
            .Include(profile => profile.Photos)
            .SingleOrDefaultAsync(profile => profile.Id == id, cancellationToken);

    private async Task<MemberProfile?> LoadLinkedProfileAsync(CancellationToken cancellationToken)
    {
        var currentUserId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(currentUserId)) return null;
        var profileId = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == currentUserId)
            .Select(user => user.MemberProfileId)
            .SingleOrDefaultAsync(cancellationToken);
        return profileId is null ? null : await LoadProfileAsync(profileId.Value, cancellationToken);
    }

    private static bool TryValidateRequest(
        MemberMutationRequest request,
        MemberProfile? existing,
        out ResidentType? residentType,
        out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>();
        residentType = null;
        if (string.IsNullOrWhiteSpace(request.GivenName)) errors[nameof(request.GivenName)] = ["Given name is required."];
        if (string.IsNullOrWhiteSpace(request.FamilyName)) errors[nameof(request.FamilyName)] = ["Family name is required."];
        if (string.IsNullOrWhiteSpace(request.HomeTown)) errors[nameof(request.HomeTown)] = ["Home town is required."];
        var today = TodayInSriLanka();
        if (request.DateOfBirth is null)
            errors[nameof(request.DateOfBirth)] = ["Date of birth is required."];
        else if (request.DateOfBirth > today)
            errors[nameof(request.DateOfBirth)] = ["Date of birth cannot be in the future."];
        else if (request.DateOfBirth < today.AddYears(-120))
            errors[nameof(request.DateOfBirth)] = ["Date of birth must be within the last 120 years."];
        if (!TryParseResidentType(request.ResidentType, out residentType) || residentType is null)
        {
            errors[nameof(request.ResidentType)] = ["Resident type must be Student or Worker."];
        }
        else if (existing?.ResidentType is not null && existing.ResidentType != residentType)
        {
            errors[nameof(request.ResidentType)] = ["Resident type cannot be changed after profile creation."];
        }

        if (residentType == ResidentType.Student)
        {
            if (request.WorkerDetails is not null) errors[nameof(request.WorkerDetails)] = ["Worker details are not valid for a Student profile."];
            if (existing is null && request.StudentDetails is null) errors[nameof(request.StudentDetails)] = ["Student details are required for a Student profile."];
        }
        else if (residentType == ResidentType.Worker)
        {
            if (request.StudentDetails is not null) errors[nameof(request.StudentDetails)] = ["Student details are not valid for a Worker profile."];
            if (existing is null && request.WorkerDetails is null) errors[nameof(request.WorkerDetails)] = ["Worker details are required for a Worker profile."];
        }

        if (request.Contacts is not null && request.Contacts.GroupBy(contact => contact.SortOrder).Any(group => group.Count() > 1))
        {
            errors[nameof(request.Contacts)] = ["Contact sort order values must be unique within a request."];
        }

        if (request.Contacts is not null)
        {
            for (var index = 0; index < request.Contacts.Count; index++)
            {
                var contact = request.Contacts[index];
                if (string.IsNullOrWhiteSpace(contact.FullName)) errors[$"contacts[{index}].fullName"] = ["Contact name is required."];
                if (string.IsNullOrWhiteSpace(contact.Relationship)) errors[$"contacts[{index}].relationship"] = ["Relationship is required."];
                if (string.IsNullOrWhiteSpace(contact.PhoneNumber) && string.IsNullOrWhiteSpace(contact.Email))
                    errors[$"contacts[{index}]"] = ["Provide a phone number or email address for each contact."];
            }
        }

        return errors.Count == 0;
    }

    private static bool TryParseResidentType(string? value, out ResidentType? residentType)
    {
        residentType = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (Enum.TryParse<ResidentType>(value, true, out var parsed) && Enum.IsDefined(parsed))
        {
            residentType = parsed;
            return true;
        }

        return false;
    }

    private static void ApplyDetails(MemberProfile profile, MemberMutationRequest request, ResidentType residentType, DateTimeOffset now)
    {
        if (residentType == ResidentType.Student && request.StudentDetails is not null)
        {
            profile.StudentDetails ??= new StudentResidentDetails { MemberProfile = profile, CreatedUtc = now };
            profile.StudentDetails.SchoolName = TrimToNull(request.StudentDetails.SchoolName);
            profile.StudentDetails.EducationLevel = TrimToNull(request.StudentDetails.EducationLevel);
            profile.StudentDetails.StudentIdentifier = TrimToNull(request.StudentDetails.StudentIdentifier);
            profile.StudentDetails.UpdatedUtc = now;
        }
        else if (residentType == ResidentType.Worker && request.WorkerDetails is not null)
        {
            profile.WorkerDetails ??= new WorkerResidentDetails { MemberProfile = profile, CreatedUtc = now };
            profile.WorkerDetails.EmployerName = TrimToNull(request.WorkerDetails.EmployerName);
            profile.WorkerDetails.JobTitle = TrimToNull(request.WorkerDetails.JobTitle);
            profile.WorkerDetails.WorkLocation = TrimToNull(request.WorkerDetails.WorkLocation);
            profile.WorkerDetails.WorkPhone = TrimToNull(request.WorkerDetails.WorkPhone);
            profile.WorkerDetails.UpdatedUtc = now;
        }
    }

    private static void AddNewContacts(MemberProfile profile, IReadOnlyList<MemberContactRequest>? contacts, DateTimeOffset now)
    {
        if (contacts is null) return;
        foreach (var contact in contacts)
        {
            profile.Contacts.Add(CreateContact(contact, now));
        }
    }

    private bool TryApplyContactChanges(
        MemberProfile profile,
        IReadOnlyList<MemberContactRequest> requests,
        DateTimeOffset now,
        out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>();
        var existingById = profile.Contacts.ToDictionary(contact => contact.Id);
        var changingIds = requests.Where(request => request.Id is not null).Select(request => request.Id!.Value).ToHashSet();
        if (changingIds.Count != requests.Count(request => request.Id is not null) || changingIds.Any(id => !existingById.ContainsKey(id)))
        {
            errors[nameof(requests)] = ["Every contact ID must belong to the profile and may be supplied once."];
            return false;
        }

        var effectiveSortOrders = profile.Contacts.Where(contact => !changingIds.Contains(contact.Id))
            .Select(contact => contact.SortOrder).Concat(requests.Select(request => request.SortOrder));
        if (effectiveSortOrders.GroupBy(sortOrder => sortOrder).Any(group => group.Count() > 1))
        {
            errors[nameof(requests)] = ["Contact sort order values must be unique for the profile."];
            return false;
        }


        var removed = profile.Contacts.Where(contact => !changingIds.Contains(contact.Id)).ToArray();
        var protectedGuardianIds = dbContext.PhotoGuardianConsents
            .Where(consent => consent.MemberProfileId == profile.Id)
            .Select(consent => consent.GuardianContactId)
            .ToHashSet();
        if (removed.Any(contact => protectedGuardianIds.Contains(contact.Id)))
        {
            errors[nameof(requests)] = ["A guardian contact with photo-consent history cannot be removed. Keep the contact and update its details instead."];
            return false;
        }

        foreach (var request in requests)
        {
            if (request.Id is null)
            {
                profile.Contacts.Add(CreateContact(request, now));
                continue;
            }

            var contact = existingById[request.Id.Value];
            ApplyContact(contact, request, now);
        }


        dbContext.MemberContacts.RemoveRange(removed);

        return true;
    }

    private static bool TryValidateSelfContacts(
        IReadOnlyList<SelfMemberContactRequest> contacts,
        out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>();
        if (contacts.GroupBy(contact => contact.SortOrder).Any(group => group.Count() > 1))
            errors[nameof(contacts)] = ["Contact sort order values must be unique."];
        for (var index = 0; index < contacts.Count; index++)
        {
            var contact = contacts[index];
            if (string.IsNullOrWhiteSpace(contact.FullName)) errors[$"contacts[{index}].fullName"] = ["Contact name is required."];
            if (string.IsNullOrWhiteSpace(contact.Relationship)) errors[$"contacts[{index}].relationship"] = ["Relationship is required."];
            if (string.IsNullOrWhiteSpace(contact.PhoneNumber) && string.IsNullOrWhiteSpace(contact.Email))
                errors[$"contacts[{index}]"] = ["Provide a phone number or email address for each contact."];
        }
        return errors.Count == 0;
    }

    private bool TryApplySelfContactChanges(
        MemberProfile profile,
        IReadOnlyList<SelfMemberContactRequest> requests,
        DateTimeOffset now,
        out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>();
        var existingById = profile.Contacts.ToDictionary(contact => contact.Id);
        var suppliedIds = requests.Where(request => request.Id is not null).Select(request => request.Id!.Value).ToArray();
        if (suppliedIds.Distinct().Count() != suppliedIds.Length || suppliedIds.Any(id => !existingById.ContainsKey(id)))
        {
            errors[nameof(requests)] = ["Every contact ID must belong to your linked profile and may be supplied once."];
            return false;
        }

        var removed = profile.Contacts.Where(contact => !suppliedIds.Contains(contact.Id)).ToArray();
        if (removed.Any(contact => contact.IsGuardian))
        {
            errors[nameof(requests)] = ["Guardian contacts are maintained by a Pastor and cannot be removed from self-service."];
            return false;
        }

        var protectedGuardianIds = dbContext.PhotoGuardianConsents
            .Where(consent => consent.MemberProfileId == profile.Id)
            .Select(consent => consent.GuardianContactId)
            .ToHashSet();
        if (removed.Any(contact => protectedGuardianIds.Contains(contact.Id)))
        {
            errors[nameof(requests)] = ["A contact with guardian-consent history cannot be removed."];
            return false;
        }

        foreach (var request in requests)
        {
            if (request.Id is null)
            {
                profile.Contacts.Add(new MemberContact
                {
                    FullName = request.FullName.Trim(),
                    Relationship = request.Relationship.Trim(),
                    PhoneNumber = TrimToNull(request.PhoneNumber),
                    Email = TrimToNull(request.Email),
                    IsEmergencyContact = request.IsEmergencyContact,
                    IsGuardian = false,
                    SortOrder = request.SortOrder,
                    CreatedUtc = now,
                    UpdatedUtc = now
                });
                continue;
            }

            var contact = existingById[request.Id.Value];
            if (contact.IsGuardian &&
                (!string.Equals(contact.FullName, request.FullName.Trim(), StringComparison.Ordinal) ||
                 !string.Equals(contact.Relationship, request.Relationship.Trim(), StringComparison.Ordinal)))
            {
                errors[nameof(requests)] = ["Guardian names and relationships are maintained by a Pastor."];
                return false;
            }

            contact.FullName = request.FullName.Trim();
            contact.Relationship = request.Relationship.Trim();
            contact.PhoneNumber = TrimToNull(request.PhoneNumber);
            contact.Email = TrimToNull(request.Email);
            contact.IsEmergencyContact = request.IsEmergencyContact;
            contact.SortOrder = request.SortOrder;
            contact.UpdatedUtc = now;
        }

        dbContext.MemberContacts.RemoveRange(removed);
        return true;
    }

    private static MemberContact CreateContact(MemberContactRequest request, DateTimeOffset now)
    {
        var contact = new MemberContact { CreatedUtc = now };
        ApplyContact(contact, request, now);
        return contact;
    }

    private static void ApplyContact(MemberContact contact, MemberContactRequest request, DateTimeOffset now)
    {
        contact.FullName = request.FullName.Trim();
        contact.Relationship = request.Relationship.Trim();
        contact.PhoneNumber = TrimToNull(request.PhoneNumber);
        contact.Email = TrimToNull(request.Email);
        contact.IsEmergencyContact = request.IsEmergencyContact;
        contact.IsGuardian = request.IsGuardian;
        contact.SortOrder = request.SortOrder;
        contact.UpdatedUtc = now;
    }

    private static void AddAuditEntry(MemberProfile profile, string actorId, string actionType, DateTimeOffset occurredUtc) =>
        profile.AuditEntries.Add(new MemberProfileAuditEntry
        {
            ActorUserId = actorId,
            ActionType = actionType,
            OccurredUtc = occurredUtc,
            CorrelationId = Guid.NewGuid()
        });

    private static MemberDetailsResponse ToDetails(MemberProfile profile, bool canManagePhoto) => new(
        profile.Id,
        profile.GivenName ?? string.Empty,
        profile.FamilyName ?? string.Empty,
        profile.PreferredName,
        profile.HomeTown,
        profile.DateOfBirth,
        profile.ResidentType,
        profile.ResidencyStatus,
        profile.ArchivedUtc,
        profile.StudentDetails is null ? null : new StudentDetailsResponse(
            profile.StudentDetails.SchoolName, profile.StudentDetails.EducationLevel, profile.StudentDetails.StudentIdentifier),
        profile.WorkerDetails is null ? null : new WorkerDetailsResponse(
            profile.WorkerDetails.EmployerName, profile.WorkerDetails.JobTitle,
            profile.WorkerDetails.WorkLocation, profile.WorkerDetails.WorkPhone),
        profile.Contacts.Select(contact => new MemberContactResponse(
            contact.Id, contact.FullName, contact.Relationship, contact.PhoneNumber, contact.Email,
            contact.IsEmergencyContact, contact.IsGuardian, contact.SortOrder)).ToArray(),
        profile.Photos.Any(photo => photo.IsCurrent && photo.ApprovalStatus == PhotoApprovalStatus.Approved),
        canManagePhoto);

    private static MemberPublicProfileResponse ToPublicProfile(MemberProfile profile) => new(
        profile.Id,
        profile.GivenName ?? string.Empty,
        profile.FamilyName ?? string.Empty,
        profile.PreferredName,
        profile.HomeTown,
        profile.ResidentType,
        profile.Photos.Any(photo => photo.IsCurrent && photo.ApprovalStatus == PhotoApprovalStatus.Approved),
        false);

    private async Task<bool> CanManagePhotoAsync(MemberProfile profile, CancellationToken cancellationToken)
    {
        if (User.IsInRole(ApplicationRoles.Pastor)) return true;
        var currentUserId = userManager.GetUserId(User);
        return !string.IsNullOrWhiteSpace(currentUserId) && await dbContext.Users.AsNoTracking()
            .AnyAsync(user => user.Id == currentUserId && user.MemberProfileId == profile.Id, cancellationToken);
    }

    private Task<bool> HasActiveGuardianConsentAsync(int memberProfileId, CancellationToken cancellationToken) =>
        dbContext.PhotoGuardianConsents.AsNoTracking().AnyAsync(
            consent => consent.MemberProfileId == memberProfileId &&
                       consent.ConsentStatus == PhotoConsentStatus.Granted && consent.WithdrawnUtc == null,
            cancellationToken);

    private static bool RequiresGuardianConsent(DateOnly? dateOfBirth)
    {
        if (dateOfBirth is null) return true;
        var today = TodayInSriLanka();
        return dateOfBirth.Value > today.AddYears(-18);
    }

    private static DateOnly TodayInSriLanka() => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, SriLankaTimeZone).DateTime);

    private static bool TryIdentifyImage(ReadOnlySpan<byte> bytes, out string contentType, out string extension)
    {
        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF &&
            bytes[^2] == 0xFF && bytes[^1] == 0xD9)
        {
            contentType = "image/jpeg";
            extension = ".jpg";
            return true;
        }
        if (bytes.Length >= 24 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) &&
            bytes.Slice(12, 4).SequenceEqual("IHDR"u8) && bytes[^8..^4].SequenceEqual("IEND"u8))
        {
            contentType = "image/png";
            extension = ".png";
            return true;
        }
        if (bytes.Length >= 20 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8) &&
            (bytes.Slice(12, 4).SequenceEqual("VP8 "u8) || bytes.Slice(12, 4).SequenceEqual("VP8L"u8) || bytes.Slice(12, 4).SequenceEqual("VP8X"u8)))
        {
            contentType = "image/webp";
            extension = ".webp";
            return true;
        }
        contentType = string.Empty;
        extension = string.Empty;
        return false;
    }

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public abstract class MemberMutationRequest
{
    [Required, StringLength(100)]
    public string GivenName { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string FamilyName { get; init; } = string.Empty;

    [StringLength(100)]
    public string? PreferredName { get; init; }

    [Required, StringLength(100)]
    public string HomeTown { get; init; } = string.Empty;

    public DateOnly? DateOfBirth { get; init; }

    [Required, StringLength(20)]
    public string ResidentType { get; init; } = string.Empty;

    public StudentDetailsRequest? StudentDetails { get; init; }

    public WorkerDetailsRequest? WorkerDetails { get; init; }

    public IReadOnlyList<MemberContactRequest>? Contacts { get; init; }
}

public sealed class CreateMemberRequest : MemberMutationRequest;
public sealed class UpdateMemberRequest : MemberMutationRequest;

public sealed class SelfMemberUpdateRequest
{
    [StringLength(100)] public string? PreferredName { get; init; }
    public IReadOnlyList<SelfMemberContactRequest> Contacts { get; init; } = [];
}

public sealed class SelfMemberContactRequest
{
    public int? Id { get; init; }
    [Required, StringLength(200)] public string FullName { get; init; } = string.Empty;
    [Required, StringLength(100)] public string Relationship { get; init; } = string.Empty;
    [StringLength(50)] public string? PhoneNumber { get; init; }
    [EmailAddress, StringLength(254)] public string? Email { get; init; }
    public bool IsEmergencyContact { get; init; }
    [Range(0, short.MaxValue)] public short SortOrder { get; init; }
}

public sealed class StudentDetailsRequest
{
    [StringLength(200)] public string? SchoolName { get; init; }
    [StringLength(100)] public string? EducationLevel { get; init; }
    [StringLength(100)] public string? StudentIdentifier { get; init; }
}

public sealed class WorkerDetailsRequest
{
    [StringLength(200)] public string? EmployerName { get; init; }
    [StringLength(150)] public string? JobTitle { get; init; }
    [StringLength(200)] public string? WorkLocation { get; init; }
    [StringLength(50)] public string? WorkPhone { get; init; }
}

public sealed class MemberContactRequest
{
    public int? Id { get; init; }
    [Required, StringLength(200)] public string FullName { get; init; } = string.Empty;
    [Required, StringLength(100)] public string Relationship { get; init; } = string.Empty;
    [StringLength(50)] public string? PhoneNumber { get; init; }
    [EmailAddress, StringLength(254)] public string? Email { get; init; }
    public bool IsEmergencyContact { get; init; }
    public bool IsGuardian { get; init; }
    [Range(0, short.MaxValue)] public short SortOrder { get; init; }
}

public sealed class GrantPhotoConsentRequest
{
    public int GuardianContactId { get; init; }
    [StringLength(512)] public string? EvidenceReference { get; init; }
    [StringLength(1000)] public string? Notes { get; init; }
}

public sealed record PhotoStateResponse(int Id, PhotoApprovalStatus ApprovalStatus, DateTimeOffset CreatedUtc, bool IsCurrent);
public sealed record PhotoConsentResponse(int Id, int GuardianContactId, DateTimeOffset GrantedUtc, string? EvidenceReference, string? Notes);
public sealed record PhotoManagementResponse(bool RequiresGuardianConsent, PhotoConsentResponse? ActiveConsent,
    PhotoStateResponse? PendingPhoto, PhotoStateResponse? CurrentPhoto);
public sealed record MemberSummaryResponse(int Id, string GivenName, string FamilyName, string? PreferredName, string? HomeTown,
    ResidentType? ResidentType, bool HasApprovedPhoto);
public sealed record MemberArchivedSummaryResponse(int Id, string GivenName, string FamilyName, string? PreferredName, string? HomeTown,
    ResidentType? ResidentType, DateTimeOffset? ArchivedUtc, bool HasApprovedPhoto);
public sealed record MemberPublicProfileResponse(int Id, string GivenName, string FamilyName, string? PreferredName, string? HomeTown,
    ResidentType? ResidentType, bool HasApprovedPhoto, bool CanManagePhoto);
public sealed record MemberProfileHistoryResponse(long Id, string ActionType, DateTimeOffset OccurredUtc, string ActorEmail);
public sealed record StudentDetailsResponse(string? SchoolName, string? EducationLevel, string? StudentIdentifier);
public sealed record WorkerDetailsResponse(string? EmployerName, string? JobTitle, string? WorkLocation, string? WorkPhone);
public sealed record MemberContactResponse(int Id, string FullName, string Relationship, string? PhoneNumber, string? Email,
    bool IsEmergencyContact, bool IsGuardian, short SortOrder);
public sealed record MemberDetailsResponse(int Id, string GivenName, string FamilyName, string? PreferredName, string? HomeTown,
    DateOnly? DateOfBirth, ResidentType? ResidentType, ResidencyStatus ResidencyStatus, DateTimeOffset? ArchivedUtc,
    StudentDetailsResponse? StudentDetails, WorkerDetailsResponse? WorkerDetails, IReadOnlyList<MemberContactResponse> Contacts,
    bool HasApprovedPhoto, bool CanManagePhoto);
