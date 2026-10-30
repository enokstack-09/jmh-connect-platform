export type DutyStatus = 1 | 2 | 3;
export interface Duty { id: number; title: string; description?: string | null; assignedMemberProfileId: number; assignedMemberDisplayName: string; scheduledForUtc: string; dueUtc?: string | null; status: DutyStatus; completedUtc?: string | null; }
export interface DutyMutation { title: string; description?: string | null; assignedMemberProfileId: number; scheduledForUtc: string; dueUtc?: string | null; }
export interface DutyAssignee { id: number; displayName: string; }
export interface DutyAudit { id: number; actionType: string; occurredUtc: string; actorEmail: string; }
