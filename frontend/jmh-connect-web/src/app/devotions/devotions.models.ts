export type DevotionSessionStatus = 1 | 2;
export type AttendanceStatus = 1 | 2 | 3;
export interface AttendanceSummary { present: number; absent: number; excused: number; unrecorded: number; }
export interface DevotionSession { id: number; title: string; scheduledStartUtc: string; durationMinutes: number; location?: string | null; notes?: string | null; status: DevotionSessionStatus; attendanceSummary?: AttendanceSummary | null; }
export interface DevotionMutation { title: string; scheduledStartUtc: string; durationMinutes: number; location?: string | null; notes?: string | null; }
export interface AttendanceRosterItem { memberProfileId: number; displayName: string; status?: AttendanceStatus | null; notes?: string | null; }
export interface MyAttendance { sessionId: number; title: string; scheduledStartUtc: string; sessionStatus: DevotionSessionStatus; status: AttendanceStatus; }
export interface DevotionAudit { id: number; actionType: string; occurredUtc: string; actorEmail: string; }
