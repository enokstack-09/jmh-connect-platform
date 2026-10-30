namespace JMHConnect.Api.Models;

public enum AnnouncementStatus : byte { Draft = 1, Published = 2, Archived = 3 }
public enum AnnouncementPriority : byte { Normal = 1, Important = 2, Urgent = 3 }
public enum AnnouncementAudience : byte { Everyone = 1, Members = 2, Leaders = 3, Pastors = 4 }
