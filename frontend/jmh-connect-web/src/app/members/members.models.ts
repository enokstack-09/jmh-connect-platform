export type ResidentType = 1 | 2;
export type ResidencyStatus = 1 | 2;

export interface MemberSummary {
  id: number;
  givenName: string;
  familyName: string;
  preferredName?: string | null;
  homeTown?: string | null;
  residentType?: ResidentType | null;
  hasApprovedPhoto: boolean;
}

export interface MemberDetails extends MemberSummary {
  dateOfBirth?: string | null;
  residencyStatus?: ResidencyStatus;
  archivedUtc?: string | null;
  studentDetails?: { schoolName?: string | null; educationLevel?: string | null; studentIdentifier?: string | null } | null;
  workerDetails?: { employerName?: string | null; jobTitle?: string | null; workLocation?: string | null; workPhone?: string | null } | null;
  contacts?: Array<{ id?: number; fullName: string; relationship: string; phoneNumber?: string | null; email?: string | null; isEmergencyContact: boolean; isGuardian: boolean; sortOrder: number }>;
  canManagePhoto?: boolean;
}

export interface ArchivedMemberSummary extends MemberSummary {
  archivedUtc?: string | null;
}

export interface MemberHistoryEntry {
  id: number;
  actionType: string;
  occurredUtc: string;
  actorEmail: string;
}

export interface MemberMutation {
  givenName: string;
  familyName: string;
  preferredName?: string | null;
  homeTown: string;
  dateOfBirth?: string | null;
  residentType: 'Student' | 'Worker';
  studentDetails?: { schoolName?: string | null; educationLevel?: string | null; studentIdentifier?: string | null };
  workerDetails?: { employerName?: string | null; jobTitle?: string | null; workLocation?: string | null; workPhone?: string | null };
  contacts?: MemberDetails['contacts'];
}

export interface SelfProfileMutation {
  preferredName?: string | null;
  contacts: Array<{
    id?: number;
    fullName: string;
    relationship: string;
    phoneNumber?: string | null;
    email?: string | null;
    isEmergencyContact: boolean;
    sortOrder: number;
  }>;
}

export type PhotoApprovalStatus = 1 | 2 | 3;

export interface PhotoState {
  id: number;
  approvalStatus: PhotoApprovalStatus;
  createdUtc: string;
  isCurrent: boolean;
}

export interface PhotoConsent {
  id: number;
  guardianContactId: number;
  grantedUtc: string;
  evidenceReference?: string | null;
  notes?: string | null;
}

export interface PhotoManagement {
  requiresGuardianConsent: boolean;
  activeConsent?: PhotoConsent | null;
  pendingPhoto?: PhotoState | null;
  currentPhoto?: PhotoState | null;
}
