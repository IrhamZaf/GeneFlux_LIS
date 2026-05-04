namespace LIS.Models;

public enum UserRole
{
    SuperAdmin = 1,
    LabAdmin = 2,
    Doctor = 3,
    LabManager = 4,
    HeadNurse = 5
}

public enum ReportStatus
{
    Draft = 0,
    PendingReview = 1,
    Approved = 2,
    Archived = 3
}

public enum Sex
{
    Male = 0,
    Female = 1
}

public enum IdentityType
{
    NRIC = 0,
    Passport = 1
}
