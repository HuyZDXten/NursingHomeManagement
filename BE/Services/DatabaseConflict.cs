using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace NursingHomeManagement.Services;

public static class DatabaseConflict
{
    public static bool IsExpected(Exception exception)
    {
        // SQL Server's execution strategy can wrap deadlocks in InvalidOperationException.
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException ||
                current is SqlException { Number: 1205 or 2601 or 2627 or 547 })
            {
                return true;
            }
        }
        return false;
    }
}
