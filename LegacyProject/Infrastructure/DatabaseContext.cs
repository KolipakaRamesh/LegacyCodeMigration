namespace LegacyProject.Infrastructure;

/// <summary>
/// Simulates a database context / unit-of-work.
/// In a production application this would wrap an ORM such as Entity Framework Core.
/// Injected as a singleton into repository constructors to represent a shared connection.
/// </summary>
public class DatabaseContext
{
    /// <summary>The connection string used to reach the backing store.</summary>
    public string ConnectionString { get; }

    /// <summary>Indicates whether the context currently has an open connection.</summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// Initializes the context with an optional connection string.
    /// </summary>
    public DatabaseContext(string connectionString = "Server=localhost;Database=OrderMgmt;Trusted_Connection=true;")
    {
        ConnectionString = connectionString;
        IsConnected = true;
    }

    /// <summary>Opens the database connection.</summary>
    public void Connect() => IsConnected = true;

    /// <summary>Closes the database connection.</summary>
    public void Disconnect() => IsConnected = false;

    /// <summary>Throws if the context is not connected.</summary>
    /// <exception cref="InvalidOperationException">Thrown when not connected.</exception>
    public void EnsureConnected()
    {
        if (!IsConnected)
            throw new InvalidOperationException("DatabaseContext is not connected.");
    }

    /// <summary>Returns a diagnostic summary of the connection state.</summary>
    public string GetConnectionInfo() =>
        $"[DatabaseContext] Connected={IsConnected} | ConnectionString={ConnectionString}";
}
