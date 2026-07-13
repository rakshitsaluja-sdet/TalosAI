// MCPBridge/Tools/DatabaseToolHandler.cs
using Microsoft.Data.SqlClient;
using McpBridge.Models;
using System.Data;
using System.Text.RegularExpressions;

namespace McpBridge.Tools;

public class DatabaseToolHandler
{
    private string? _connectionString;

    private static readonly Regex ValidIdentifier =
        new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    private static readonly string[] DangerousKeywords =
    {
        "DROP", "ALTER", "TRUNCATE", "EXEC ", "EXECUTE ", "XP_", "SP_CONFIGURE",
        "SHUTDOWN", "GRANT", "REVOKE", "DENY"
    };

    // ── Identifier / query safety helpers ───────────────────────────────
    private static void EnsureValidIdentifier(string name, string kind)
    {
        if (!ValidIdentifier.IsMatch(name))
            throw new ArgumentException(
                $"Invalid {kind} name: '{name}'. Only letters, digits, and underscores are allowed.");
    }

    /// <summary>
    /// Blocks schema/permission-altering keywords and multi-statement chaining
    /// unconditionally. Optionally requires the query to be a SELECT unless the
    /// caller explicitly opts into write access via 'allow_write'.
    /// </summary>
    private static void EnsureQuerySafe(string query, bool allowWrite, bool requireSelectUnlessWrite)
    {
        var upper = query.ToUpperInvariant();
        foreach (var keyword in DangerousKeywords)
        {
            if (upper.Contains(keyword))
                throw new InvalidOperationException(
                    $"Query contains a disallowed keyword ('{keyword.Trim()}'). " +
                    "This tool does not permit schema/permission-altering statements.");
        }

        var trimmed = query.Trim().TrimEnd(';');
        if (trimmed.Contains(';'))
            throw new InvalidOperationException("Multiple statements in a single query are not allowed.");

        if (requireSelectUnlessWrite && !allowWrite &&
            !trimmed.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Only SELECT queries are allowed unless 'allow_write' is explicitly set to true.");
        }
    }

    private static bool GetAllowWrite(Dictionary<string, object> args) =>
        args.TryGetValue("allow_write", out var v) && bool.TryParse(v?.ToString(), out var b) && b;

    /// <summary>
    /// Builds a parameterized WHERE clause from a column→value dictionary,
    /// validating every column name against the identifier allow-list. Avoids
    /// ever concatenating caller-supplied SQL fragments into the query text.
    /// </summary>
    private static (string Clause, List<SqlParameter> Parameters) BuildWhereClause(
        Dictionary<string, object>? where)
    {
        if (where == null || where.Count == 0)
            return ("1=1", new List<SqlParameter>());

        var clauses = new List<string>();
        var parameters = new List<SqlParameter>();
        int i = 0;
        foreach (var kvp in where)
        {
            EnsureValidIdentifier(kvp.Key, "column");
            var paramName = $"@where{i}";
            clauses.Add($"{kvp.Key} = {paramName}");
            parameters.Add(new SqlParameter(paramName, kvp.Value ?? DBNull.Value));
            i++;
        }
        return (string.Join(" AND ", clauses), parameters);
    }

    // ── Configure Database ──────────────────────────────────────────────
    public ToolResponse ConfigureDatabase(Dictionary<string, object> args)
    {
        var server = args.GetValueOrDefault("server")?.ToString();
        var database = args.GetValueOrDefault("database")?.ToString();
        var username = args.GetValueOrDefault("username")?.ToString();
        var password = args.GetValueOrDefault("password")?.ToString();
        var integratedSecurity = args.ContainsKey("integrated_security")
            ? bool.Parse(args["integrated_security"].ToString()!)
            : false;

        if (args.ContainsKey("connection_string"))
        {
            _connectionString = args["connection_string"].ToString();
        }
        else if (integratedSecurity)
        {
            _connectionString = $"Server={server};Database={database};Integrated Security=true;TrustServerCertificate=true;";
        }
        else
        {
            _connectionString = $"Server={server};Database={database};User Id={username};Password={password};TrustServerCertificate=true;";
        }

        return ToolResponse.Ok(new
        {
            status = "ok",
            server,
            database,
            integratedSecurity
        });
    }

    // ── Execute Query (read-only unless allow_write) ────────────────────
    public ToolResponse ExecuteQuery(Dictionary<string, object> args)
    {
        var query = args["query"].ToString()!;
        var maxRows = args.ContainsKey("max_rows") ? int.Parse(args["max_rows"].ToString()!) : 100;
        maxRows = Math.Clamp(maxRows, 1, 10_000);

        try
        {
            EnsureQuerySafe(query, GetAllowWrite(args), requireSelectUnlessWrite: true);

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = new SqlCommand(query, connection);
            command.CommandTimeout = 30;

            using var reader = command.ExecuteReader();

            var results = new List<Dictionary<string, object>>();
            var columnNames = new List<string>();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                columnNames.Add(reader.GetName(i));
            }

            int rowCount = 0;
            while (reader.Read() && rowCount < maxRows)
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null! : reader.GetValue(i);
                }
                results.Add(row);
                rowCount++;
            }

            return ToolResponse.Ok(new
            {
                status = "ok",
                rowCount = results.Count,
                columns = columnNames,
                data = results
            });
        }
        catch (Exception ex)
        {
            return ToolResponse.Fail($"Query execution failed: {ex.Message}");
        }
    }

    // ── Execute Non-Query (INSERT/UPDATE/DELETE) — requires allow_write ─
    public ToolResponse ExecuteNonQuery(Dictionary<string, object> args)
    {
        var query = args["query"].ToString()!;

        try
        {
            if (!GetAllowWrite(args))
                throw new InvalidOperationException(
                    "execute_non_query performs a write. Set 'allow_write': true to confirm.");

            EnsureQuerySafe(query, allowWrite: true, requireSelectUnlessWrite: false);

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = new SqlCommand(query, connection);
            command.CommandTimeout = 30;

            var rowsAffected = command.ExecuteNonQuery();

            return ToolResponse.Ok(new
            {
                status = "ok",
                rowsAffected
            });
        }
        catch (Exception ex)
        {
            return ToolResponse.Fail($"Non-query execution failed: {ex.Message}");
        }
    }

    // ── Execute Scalar (COUNT, MAX, etc. — read-only unless allow_write) ─
    public ToolResponse ExecuteScalar(Dictionary<string, object> args)
    {
        var query = args["query"].ToString()!;

        try
        {
            EnsureQuerySafe(query, GetAllowWrite(args), requireSelectUnlessWrite: true);

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = new SqlCommand(query, connection);
            command.CommandTimeout = 30;

            var result = command.ExecuteScalar();

            return ToolResponse.Ok(new
            {
                status = "ok",
                result = result?.ToString()
            });
        }
        catch (Exception ex)
        {
            return ToolResponse.Fail($"Scalar query failed: {ex.Message}");
        }
    }

    // ── Verify Data Exists ───────────────────────────────────────────────
    public ToolResponse VerifyDataExists(Dictionary<string, object> args)
    {
        var table = args["table"].ToString()!;

        try
        {
            EnsureValidIdentifier(table, "table");
            var whereDict = args.GetValueOrDefault("where") as Dictionary<string, object>;
            var (clause, parameters) = BuildWhereClause(whereDict);
            var query = $"SELECT COUNT(*) FROM {table} WHERE {clause}";

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddRange(parameters.ToArray());
            var count = (int)command.ExecuteScalar()!;

            return ToolResponse.Ok(new
            {
                status = "ok",
                table,
                exists = count > 0,
                count
            });
        }
        catch (Exception ex)
        {
            return ToolResponse.Fail($"Data verification failed: {ex.Message}");
        }
    }

    // ── Insert Test Data ─────────────────────────────────────────────────
    public ToolResponse InsertTestData(Dictionary<string, object> args)
    {
        var table = args["table"].ToString()!;
        var data = args["data"] as Dictionary<string, object>;

        if (data == null || data.Count == 0)
            return ToolResponse.Fail("No data provided for insertion");

        try
        {
            EnsureValidIdentifier(table, "table");
            foreach (var key in data.Keys)
                EnsureValidIdentifier(key, "column");

            var columns = string.Join(", ", data.Keys);
            var parameters = string.Join(", ", data.Keys.Select(k => $"@{k}"));
            var query = $"INSERT INTO {table} ({columns}) VALUES ({parameters}); SELECT SCOPE_IDENTITY();";

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = new SqlCommand(query, connection);

            foreach (var kvp in data)
            {
                command.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value ?? DBNull.Value);
            }

            var insertedId = command.ExecuteScalar();

            return ToolResponse.Ok(new
            {
                status = "ok",
                table,
                insertedId = insertedId?.ToString()
            });
        }
        catch (Exception ex)
        {
            return ToolResponse.Fail($"Insert failed: {ex.Message}");
        }
    }

    // ── Delete Test Data ─────────────────────────────────────────────────
    public ToolResponse DeleteTestData(Dictionary<string, object> args)
    {
        var table = args["table"].ToString()!;

        try
        {
            EnsureValidIdentifier(table, "table");
            var whereDict = args.GetValueOrDefault("where") as Dictionary<string, object>;

            // Require an explicit, non-empty WHERE to avoid accidental full-table deletes.
            if (whereDict == null || whereDict.Count == 0)
                return ToolResponse.Fail(
                    "delete_test_data requires a non-empty 'where' (column→value) to avoid deleting an entire table.");

            var (clause, parameters) = BuildWhereClause(whereDict);
            var query = $"DELETE FROM {table} WHERE {clause}";

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddRange(parameters.ToArray());
            var rowsDeleted = command.ExecuteNonQuery();

            return ToolResponse.Ok(new
            {
                status = "ok",
                table,
                rowsDeleted
            });
        }
        catch (Exception ex)
        {
            return ToolResponse.Fail($"Delete failed: {ex.Message}");
        }
    }

    // ── Get Table Schema ─────────────────────────────────────────────────
    public ToolResponse GetTableSchema(Dictionary<string, object> args)
    {
        var table = args["table"].ToString()!;

        var query = @"
            SELECT
                COLUMN_NAME as Name,
                DATA_TYPE as Type,
                CHARACTER_MAXIMUM_LENGTH as MaxLength,
                IS_NULLABLE as IsNullable
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @TableName
            ORDER BY ORDINAL_POSITION";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@TableName", table);

            using var reader = command.ExecuteReader();

            var columns = new List<object>();
            while (reader.Read())
            {
                columns.Add(new
                {
                    name = reader["Name"].ToString(),
                    type = reader["Type"].ToString(),
                    maxLength = reader["MaxLength"] == DBNull.Value ? null : reader["MaxLength"].ToString(),
                    isNullable = reader["IsNullable"].ToString() == "YES"
                });
            }

            return ToolResponse.Ok(new
            {
                status = "ok",
                table,
                columnCount = columns.Count,
                columns
            });
        }
        catch (Exception ex)
        {
            return ToolResponse.Fail($"Schema retrieval failed: {ex.Message}");
        }
    }

    // ── Execute Stored Procedure — requires allow_write ─────────────────
    public ToolResponse ExecuteStoredProcedure(Dictionary<string, object> args)
    {
        var procedureName = args["procedure_name"].ToString()!;
        var parameters = args.ContainsKey("parameters")
            ? args["parameters"] as Dictionary<string, object>
            : new Dictionary<string, object>();

        try
        {
            if (!GetAllowWrite(args))
                throw new InvalidOperationException(
                    "execute_stored_procedure can perform arbitrary actions. Set 'allow_write': true to confirm.");
            EnsureValidIdentifier(procedureName, "procedure");

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = new SqlCommand(procedureName, connection);
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 30;

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue($"@{param.Key}", param.Value ?? DBNull.Value);
                }
            }

            using var reader = command.ExecuteReader();

            var results = new List<Dictionary<string, object>>();
            while (reader.Read())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null! : reader.GetValue(i);
                }
                results.Add(row);
            }

            return ToolResponse.Ok(new
            {
                status = "ok",
                procedureName,
                rowCount = results.Count,
                data = results
            });
        }
        catch (Exception ex)
        {
            return ToolResponse.Fail($"Stored procedure execution failed: {ex.Message}");
        }
    }

    // ── Backup Test Data ─────────────────────────────────────────────────
    public ToolResponse BackupTestData(Dictionary<string, object> args)
    {
        var table = args["table"].ToString()!;

        try
        {
            EnsureValidIdentifier(table, "table");
            var whereDict = args.GetValueOrDefault("where") as Dictionary<string, object>;
            var (clause, parameters) = BuildWhereClause(whereDict);
            var backupTable = $"{table}_Backup";
            var query = $"SELECT * INTO {backupTable} FROM {table} WHERE {clause}";

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddRange(parameters.ToArray());
            command.ExecuteNonQuery();

            return ToolResponse.Ok(new
            {
                status = "ok",
                originalTable = table,
                backupTable
            });
        }
        catch (Exception ex)
        {
            return ToolResponse.Fail($"Backup failed: {ex.Message}");
        }
    }
}
