namespace JMHConnect.Api.Models;

public enum ResidentType : byte
{
    Student = 1,
    Worker = 2
}

public enum ResidencyStatus : byte
{
    Active = 1,
    Archived = 2
}

public enum PhotoApprovalStatus : byte
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public enum PhotoConsentStatus : byte
{
    Granted = 1,
    Withdrawn = 2
}
