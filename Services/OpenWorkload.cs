using System.Linq.Expressions;
using MMGT.API.Models;

namespace MMGT.API.Services
{
    /// <summary>
    /// One definition of reviewer workload, used everywhere.
    /// Open workload = an active assignment on an application that is submitted, under review or pending, and not locked.
    /// Decided applications keep their assignment (so reports show who decided them) but are not workload.
    /// </summary>
    public static class OpenWorkload
    {
        public static readonly Expression<Func<ApplicationAssignment, bool>> Open = a =>
            a.IsActive
            && a.Application != null
            && !a.Application.IsLocked
            && (a.Application.Status == "submitted"
                || a.Application.Status == "under_review"
                || a.Application.Status == "pending"
                || a.Application.Status == "returned"
                || a.Application.Status == "resubmitted");
                /// <summary>Any active assignment, whatever the application's status. Used only to FIND applications (view only), never for workload counts.</summary>
                public static readonly Expression<Func<ApplicationAssignment, bool>> Active = a => a.IsActive;
    }
}