export type EventStatus = 1 | 2;
export interface CommunityEvent { id: number; title: string; description?: string | null; startsUtc: string; endsUtc: string; location?: string | null; status: EventStatus; }
export interface CommunityEventMutation { title: string; description?: string | null; startsUtc: string; endsUtc: string; location?: string | null; }
export interface CommunityEventAudit { id: number; actionType: string; occurredUtc: string; actorEmail: string; }
