using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ERPTrustSolution.Services;

public class DbService : IDbService
{
    private readonly string _defaultConn;

    public DbService(IConfiguration config)
        => _defaultConn = config.GetConnectionString("SqlConnection")!;

    // ── helpers ──────────────────────────────────────────────────────────
    private SqlConnection Open(string? conn = null)
        => new SqlConnection(conn ?? _defaultConn);

    private static async Task<DataTable> ReaderToDataTable(SqlConnection db, string sql, object? param, CommandType ct)
    {
        var reader = await db.ExecuteReaderAsync(sql, param, commandType: ct);
        var dt = new DataTable();
        dt.Load(reader);
        return dt;
    }

    private static async Task<DataSet> ReaderToDataSet(SqlConnection db, string sql, object? param, CommandType ct)
    {
        var ds = new DataSet();
        using var cmd = new SqlCommand(sql, db);
        cmd.CommandType = ct;
        if (param != null)
            foreach (var prop in param.GetType().GetProperties())
                cmd.Parameters.AddWithValue("@" + prop.Name, prop.GetValue(param) ?? DBNull.Value);
        using var adapter = new SqlDataAdapter(cmd);
        await Task.Run(() => adapter.Fill(ds));
        return ds;
    }

    // ── Trust DB (default connection) ────────────────────────────────────
    public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, CommandType ct = CommandType.Text)
    { using var db = Open(); return await db.QueryFirstOrDefaultAsync<T>(sql, param, commandType: ct); }

    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, CommandType ct = CommandType.Text)
    { using var db = Open(); return await db.QueryAsync<T>(sql, param, commandType: ct); }

    public async Task<int> ExecuteAsync(string sql, object? param = null, CommandType ct = CommandType.Text)
    { using var db = Open(); return await db.ExecuteAsync(sql, param, commandType: ct); }

    public async Task<object?> ExecuteScalarAsync(string sql, object? param = null, CommandType ct = CommandType.Text)
    { using var db = Open(); return await db.ExecuteScalarAsync(sql, param, commandType: ct); }

    public async Task<DataTable> GetDataTableAsync(string sql, object? param = null, CommandType ct = CommandType.Text)
    { using var db = Open(); await db.OpenAsync(); return await ReaderToDataTable(db, sql, param, ct); }

    public async Task<DataSet> GetDataSetAsync(string sql, object? param = null, CommandType ct = CommandType.Text)
    { using var db = Open(); await db.OpenAsync(); return await ReaderToDataSet(db, sql, param, ct); }

    // ── College DB (per-college dynamic connection string) ────────────────
    public async Task<T?> QueryFirstOrDefaultAsync<T>(string connectionString, string sql, object? param = null, CommandType ct = CommandType.Text)
    { using var db = Open(connectionString); return await db.QueryFirstOrDefaultAsync<T>(sql, param, commandType: ct); }

    public async Task<IEnumerable<T>> QueryAsync<T>(string connectionString, string sql, object? param = null, CommandType ct = CommandType.Text)
    { using var db = Open(connectionString); return await db.QueryAsync<T>(sql, param, commandType: ct); }

    public async Task<int> ExecuteAsync(string connectionString, string sql, object? param = null, CommandType ct = CommandType.Text)
    { using var db = Open(connectionString); return await db.ExecuteAsync(sql, param, commandType: ct); }

    public async Task<object?> ExecuteScalarAsync(string connectionString, string sql, object? param = null, CommandType ct = CommandType.Text)
    { using var db = Open(connectionString); return await db.ExecuteScalarAsync(sql, param, commandType: ct); }

    public async Task<DataTable> GetDataTableAsync(string connectionString, string sql, object? param = null, CommandType ct = CommandType.Text)
    { using var db = Open(connectionString); await db.OpenAsync(); return await ReaderToDataTable(db, sql, param, ct); }

    public async Task<DataSet> GetDataSetAsync(string connectionString, string sql, object? param = null, CommandType ct = CommandType.Text)
    { using var db = Open(connectionString); await db.OpenAsync(); return await ReaderToDataSet(db, sql, param, ct); }
}
