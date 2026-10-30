export type AnnouncementStatus = 1 | 2 | 3; export type AnnouncementPriority = 1 | 2 | 3; export type AnnouncementAudience = 1 | 2 | 3 | 4;
export interface Announcement { id:number; title:string; body:string; priority:AnnouncementPriority; audience:AnnouncementAudience; status:AnnouncementStatus; publishedUtc:string|null; archivedUtc:string|null; createdUtc:string; updatedUtc:string; }
export interface AnnouncementMutation { title:string; body:string; priority:AnnouncementPriority; audience:AnnouncementAudience; }
