using System.Data;

namespace ERPTrustSolution.Services;

/// <summary>
/// Builds and caches the per-college connection string from TrustMaster.
/// Used by Society controllers that connect to a college's own database.
/// Cache key is stored in the controller's in-memory dictionary per request.
/// </summary>
public interface ICollegeConnectionService
{
    Task<string> GetConnectionStringAsync(int collegeId);
}

public class CollegeConnectionService : ICollegeConnectionService
{
    private readonly IDbService _db;

    public CollegeConnectionService(IDbService db) => _db = db;

    public async Task<string> GetConnectionStringAsync(int collegeId)
    {
        var row = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT DataSource, InitialCatalog, UserID, Password FROM TrustMaster WHERE Id = @Id",
            new { Id = collegeId });

        if (row == null)
            throw new InvalidOperationException($"College with Id={collegeId} not found in TrustMaster.");

        return $"Data Source={row.DataSource};Initial Catalog={row.InitialCatalog};" +
               $"User ID={row.UserID};Password={row.Password};Connection Timeout=3600;" +
               $"TrustServerCertificate=True";
    }
}
