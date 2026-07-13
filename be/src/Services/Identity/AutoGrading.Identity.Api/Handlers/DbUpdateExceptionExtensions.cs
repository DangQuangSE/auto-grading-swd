using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Identity.Api.Handlers;

/// <summary>Distinguishes a primary-key/unique-constraint violation (expected on concurrent redelivery of
/// the same event) from any other DbUpdateException cause, which handlers must not swallow.</summary>
public static class DbUpdateExceptionExtensions
{
    public static bool IsPrimaryKeyViolation(this DbUpdateException ex) =>
        ex.InnerException is SqlException sqlEx && (sqlEx.Number is 2627 or 2601);
}
