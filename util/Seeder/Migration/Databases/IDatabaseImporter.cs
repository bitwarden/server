namespace Bit.Seeder.Migration.Databases;

/// <summary>
/// Interface for database importers that migrate data from SQL Server to various database systems.
/// </summary>
public interface IDatabaseImporter : IDisposable
{
    /// <summary>
    /// Establishes a connection to the target database.
    /// </summary>
    /// <returns>True if connection was successful, false otherwise.</returns>
    bool Connect();

    /// <summary>
    /// Closes the connection to the target database.
    /// </summary>
    void Disconnect();

    /// <summary>
    /// Creates a table in the target database from a schema definition.
    /// </summary>
    /// <param name="tableName">Name of the table to create.</param>
    /// <param name="columns">List of column names.</param>
    /// <param name="columnTypes">Dictionary mapping column names to their SQL Server data types.</param>
    /// <param name="specialColumns">Optional list of columns that require special handling (e.g., JSON columns).</param>
    /// <returns>True if table was created successfully, false otherwise.</returns>
    bool CreateTableFromSchema(
        string tableName,
        List<string> columns,
        Dictionary<string, string> columnTypes,
        List<string>? specialColumns = null);

    /// <summary>
    /// Retrieves the list of column names for a table.
    /// </summary>
    /// <param name="tableName">Name of the table.</param>
    /// <returns>List of column names.</returns>
    List<string> GetTableColumns(string tableName);

    /// <summary>
    /// Imports data into a table.
    /// </summary>
    /// <param name="tableName">Name of the target table.</param>
    /// <param name="columns">List of column names in the data.</param>
    /// <param name="data">Data rows to import.</param>
    /// <param name="batchSize">Number of rows to import per batch.</param>
    /// <returns>True if import was successful, false otherwise.</returns>
    bool ImportData(
        string tableName,
        List<string> columns,
        List<object[]> data,
        int batchSize = 1000);

    /// <summary>
    /// Checks if a table exists in the target database.
    /// </summary>
    /// <param name="tableName">Name of the table to check.</param>
    /// <returns>True if table exists, false otherwise.</returns>
    bool TableExists(string tableName);

    /// <summary>
    /// Gets the number of rows in a table.
    /// </summary>
    /// <param name="tableName">Name of the table.</param>
    /// <returns>Number of rows in the table.</returns>
    int GetTableRowCount(string tableName);

    /// <summary>
    /// Drops a table from the database.
    /// </summary>
    /// <param name="tableName">Name of the table to drop.</param>
    /// <returns>True if table was dropped successfully, false otherwise.</returns>
    bool DropTable(string tableName);

    /// <summary>
    /// Disables foreign key constraints to allow data import without referential integrity checks.
    /// </summary>
    /// <returns>True if foreign keys were disabled successfully, false otherwise.</returns>
    bool DisableForeignKeys();

    /// <summary>
    /// Re-enables foreign key constraints after data import.
    /// </summary>
    /// <returns>True if foreign keys were enabled successfully, false otherwise.</returns>
    bool EnableForeignKeys();

    /// <summary>
    /// Checks if this importer supports optimized bulk copy operations.
    /// </summary>
    /// <returns>True if bulk copy is supported and should be preferred over row-by-row import.</returns>
    bool SupportsBulkCopy();

    /// <summary>
    /// Imports data into a table using database-specific bulk copy operations for optimal performance.
    /// This method uses native bulk import mechanisms like PostgreSQL COPY, SQL Server SqlBulkCopy,
    /// or multi-row INSERT statements for databases that support them.
    /// </summary>
    /// <param name="tableName">Name of the target table.</param>
    /// <param name="columns">List of column names in the data.</param>
    /// <param name="data">Data rows to import.</param>
    /// <returns>True if bulk import was successful, false otherwise.</returns>
    /// <remarks>
    /// This method is significantly faster than ImportData() for large datasets (10-100x speedup).
    /// If this method returns false, the caller should fall back to ImportData().
    /// </remarks>
    bool ImportDataBulk(
        string tableName,
        List<string> columns,
        List<object[]> data);

    /// <summary>
    /// Tests the connection to the database.
    /// </summary>
    /// <returns>True if connection test was successful, false otherwise.</returns>
    bool TestConnection();
}
