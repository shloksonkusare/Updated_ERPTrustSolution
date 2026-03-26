using System.Data;

namespace ERPTrustSolution.Services;

public interface IDbService
{
    // Trust DB (main connection from appsettings.json)
    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, CommandType commandType = CommandType.Text);
    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, CommandType commandType = CommandType.Text);
    Task<int> ExecuteAsync(string sql, object? param = null, CommandType commandType = CommandType.Text);
    Task<object?> ExecuteScalarAsync(string sql, object? param = null, CommandType commandType = CommandType.Text);
    Task<DataTable> GetDataTableAsync(string sql, object? param = null, CommandType commandType = CommandType.Text);
    Task<DataSet> GetDataSetAsync(string sql, object? param = null, CommandType commandType = CommandType.Text);

    // College DB (dynamic per-college connection string from TrustMaster)
    Task<DataTable> GetDataTableAsync(string connectionString, string sql, object? param = null, CommandType commandType = CommandType.Text);
    Task<DataSet> GetDataSetAsync(string connectionString, string sql, object? param = null, CommandType commandType = CommandType.Text);
    Task<T?> QueryFirstOrDefaultAsync<T>(string connectionString, string sql, object? param = null, CommandType commandType = CommandType.Text);
    Task<IEnumerable<T>> QueryAsync<T>(string connectionString, string sql, object? param = null, CommandType commandType = CommandType.Text);
    Task<int> ExecuteAsync(string connectionString, string sql, object? param = null, CommandType commandType = CommandType.Text);
    Task<object?> ExecuteScalarAsync(string connectionString, string sql, object? param = null, CommandType commandType = CommandType.Text);
}
