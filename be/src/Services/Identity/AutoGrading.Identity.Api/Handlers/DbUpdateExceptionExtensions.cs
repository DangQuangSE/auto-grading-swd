using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AutoGrading.Identity.Api.Handlers;

public static class DbUpdateExceptionExtensions
{
    public static string GetPostgresErrorDetail(this DbUpdateException ex)
    {
        if (ex.InnerException is PostgresException pgEx)
        {
            return pgEx.MessageText;
        }
        if (ex.InnerException?.InnerException is PostgresException pgEx2)
        {
            return pgEx2.MessageText;
        }
        return ex.Message;
    }
}
