using System.Data;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.AdminConsole.Helpers;

public static class BulkResourceCreationService
{
    private const string _defaultErrorMessage = "Must have at least one record for bulk creation.";
    public static async Task CreateCollectionsUsersAsync(SqlConnection connection, SqlTransaction transaction, IEnumerable<CollectionUser> collectionUsers, string errorMessage = _defaultErrorMessage)
    {
        using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction);
        bulkCopy.DestinationTableName = "[dbo].[CollectionUser]";
        var dataTable = BuildCollectionsUsersTable(bulkCopy, collectionUsers, errorMessage);
        await bulkCopy.WriteToServerAsync(dataTable);
    }

    private static DataTable BuildCollectionsUsersTable(SqlBulkCopy bulkCopy, IEnumerable<CollectionUser> collectionUsers, string errorMessage)
    {
        var collectionUser = collectionUsers.First();

        if (collectionUser == null)
        {
            throw new ApplicationException(errorMessage);
        }

        var table = new DataTable("CollectionUserDataTable");

        var collectionIdColumn = new DataColumn(nameof(collectionUser.CollectionId), typeof(Guid));
        table.Columns.Add(collectionIdColumn);

        var orgUserIdColumn = new DataColumn(nameof(collectionUser.OrganizationUserId), typeof(Guid));
        table.Columns.Add(orgUserIdColumn);

        var readOnlyColumn = new DataColumn(nameof(collectionUser.ReadOnly), typeof(bool));
        table.Columns.Add(readOnlyColumn);

        var hidePasswordsColumn = new DataColumn(nameof(collectionUser.HidePasswords), typeof(bool));
        table.Columns.Add(hidePasswordsColumn);

        var manageColumn = new DataColumn(nameof(collectionUser.Manage), typeof(bool));
        table.Columns.Add(manageColumn);

        foreach (DataColumn col in table.Columns)
        {
            bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        }

        foreach (var collectionUserRecord in collectionUsers)
        {
            var row = table.NewRow();

            row[collectionIdColumn] = collectionUserRecord.CollectionId;
            row[orgUserIdColumn] = collectionUserRecord.OrganizationUserId;
            row[readOnlyColumn] = collectionUserRecord.ReadOnly;
            row[hidePasswordsColumn] = collectionUserRecord.HidePasswords;
            row[manageColumn] = collectionUserRecord.Manage;

            table.Rows.Add(row);
        }

        return table;
    }

    public static async Task CreateCollectionsAsync(SqlConnection connection, SqlTransaction transaction, IEnumerable<Collection> collections, string errorMessage = _defaultErrorMessage)
    {
        using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction);
        bulkCopy.DestinationTableName = "[dbo].[Collection]";
        var dataTable = BuildCollectionsTable(bulkCopy, collections, errorMessage);
        await bulkCopy.WriteToServerAsync(dataTable);
    }

    private static DataTable BuildCollectionsTable(SqlBulkCopy bulkCopy, IEnumerable<Collection> collections, string errorMessage)
    {
        var collection = collections.First();

        if (collection == null)
        {
            throw new ApplicationException(errorMessage);
        }

        var collectionsTable = new DataTable("CollectionDataTable");

        var idColumn = new DataColumn(nameof(collection.Id), collection.Id.GetType());
        collectionsTable.Columns.Add(idColumn);
        var organizationIdColumn = new DataColumn(nameof(collection.OrganizationId), collection.OrganizationId.GetType());
        collectionsTable.Columns.Add(organizationIdColumn);
        var nameColumn = new DataColumn(nameof(collection.Name), typeof(string));
        collectionsTable.Columns.Add(nameColumn);
        var creationDateColumn = new DataColumn(nameof(collection.CreationDate), collection.CreationDate.GetType());
        collectionsTable.Columns.Add(creationDateColumn);
        var revisionDateColumn = new DataColumn(nameof(collection.RevisionDate), collection.RevisionDate.GetType());
        collectionsTable.Columns.Add(revisionDateColumn);
        var externalIdColumn = new DataColumn(nameof(collection.ExternalId), typeof(string));
        collectionsTable.Columns.Add(externalIdColumn);
        var typeColumn = new DataColumn(nameof(collection.Type), typeof(CollectionType));
        collectionsTable.Columns.Add(typeColumn);
        var defaultUserCollectionEmailColumn = new DataColumn(nameof(collection.DefaultUserCollectionEmail), typeof(string));
        collectionsTable.Columns.Add(defaultUserCollectionEmailColumn);

        foreach (DataColumn col in collectionsTable.Columns)
        {
            bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        }

        var keys = new DataColumn[1];
        keys[0] = idColumn;
        collectionsTable.PrimaryKey = keys;

        foreach (var collectionRecord in collections)
        {
            var row = collectionsTable.NewRow();

            row[idColumn] = collectionRecord.Id;
            row[organizationIdColumn] = collectionRecord.OrganizationId;
            row[nameColumn] = collectionRecord.Name;
            row[creationDateColumn] = collectionRecord.CreationDate;
            row[revisionDateColumn] = collectionRecord.RevisionDate;
            row[externalIdColumn] = collectionRecord.ExternalId;
            row[typeColumn] = collectionRecord.Type;
            row[defaultUserCollectionEmailColumn] = collectionRecord.DefaultUserCollectionEmail;

            collectionsTable.Rows.Add(row);
        }

        return collectionsTable;
    }
}
