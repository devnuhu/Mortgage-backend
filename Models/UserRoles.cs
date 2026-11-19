namespace MMGT.API.Models
{
    public sealed class RoleInfo
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Every role a user can hold. The frontend fills its role dropdown from GET api/roles,
    /// and creating a user refuses any value that is not in this list.
    /// Role values are matched exactly (capital letters included), so they are never typed by hand.
    /// </summary>
    public static class UserRoles
    {
        public static readonly RoleInfo[] All = new[]
        {
            new RoleInfo { Value = "SUPER_ADMIN",          Label = "Super Admin",                          Description = "Manages users and system settings" },
            new RoleInfo { Value = "ADMIN",                Label = "Admin",                                Description = "Reviews applications" },
            new RoleInfo { Value = "REVIEWER",             Label = "Reviewer",                             Description = "Reviews assigned applications" },
            new RoleInfo { Value = "LEAD_REVIEWER",        Label = "Lead Reviewer",                        Description = "Assigns and reassigns reviewers, and raises step-down requests" },
            new RoleInfo { Value = "CLIENT",               Label = "Client",                               Description = "Submits applications" },
            new RoleInfo { Value = "PMI",                  Label = "PMI",                                  Description = "Pension manager institution user" },
            new RoleInfo { Value = StepDownRoles.Maker,    Label = "Service Manager",                      Description = "Customer service team: looks up any application and creates and edits applications for clients" },
            new RoleInfo { Value = StepDownRoles.Approver, Label = "Head of Benefit Administration",       Description = "Approves step-down requests" },
            new RoleInfo { Value = StepDownRoles.Checker,  Label = "Mortgage Lead (step-down checker)",    Description = "Reviews and steps applications down, and restores them" }
        };

        public static IEnumerable<string> Values => All.Select(r => r.Value);

        public static bool IsValid(string? role) =>
            !string.IsNullOrEmpty(role) && All.Any(r => string.Equals(r.Value, role, StringComparison.Ordinal));
    }
}
