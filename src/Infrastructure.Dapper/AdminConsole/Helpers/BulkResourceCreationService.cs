using System.Data;
using Bit.Core.Entities;
using Bit.Core.Vault.Entities;
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

    public static async Task CreateCiphersAsync(SqlConnection connection, SqlTransaction transaction, IEnumerable<Cipher> ciphers, string errorMessage = _defaultErrorMessage)
    {
        using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction);
        bulkCopy.DestinationTableName = "[dbo].[Cipher]";
        var dataTable = BuildCiphersTable(bulkCopy, ciphers, errorMessage);
        await bulkCopy.WriteToServerAsync(dataTable);
    }

    public static async Task CreateFoldersAsync(SqlConnection connection, SqlTransaction transaction, IEnumerable<Folder> folders, string errorMessage = _defaultErrorMessage)
    {
        using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction);
        bulkCopy.DestinationTableName = "[dbo].[Folder]";
        var dataTable = BuildFoldersTable(bulkCopy, folders, errorMessage);
        await bulkCopy.WriteToServerAsync(dataTable);
    }

    public static async Task CreateCollectionCiphersAsync(SqlConnection connection, SqlTransaction transaction, IEnumerable<CollectionCipher> collectionCiphers, string errorMessage = _defaultErrorMessage)
    {
        using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction);
        bulkCopy.DestinationTableName = "[dbo].[CollectionCipher]";
        var dataTable = BuildCollectionCiphersTable(bulkCopy, collectionCiphers, errorMessage);
        await bulkCopy.WriteToServerAsync(dataTable);
    }

    public static async Task CreateTempCiphersAsync(SqlConnection connection, SqlTransaction transaction, IEnumerable<Cipher> ciphers, string errorMessage = _defaultErrorMessage)
    {
        using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction);
        bulkCopy.DestinationTableName = "#TempCipher";
        var dataTable = BuildCiphersTable(bulkCopy, ciphers, errorMessage);
        await bulkCopy.WriteToServerAsync(dataTable);
    }

    private static DataTable BuildCollectionsUsersTable(SqlBulkCopy bulkCopy, IEnumerable<CollectionUser> collectionUsers, string errorMessage)
    {
        var collectionUser = collectionUsers.FirstOrDefault();

        if (collectionUser == null)
        {
            throw new ApplicationException(errorMessage);
        }

        var table = new DataTable("CollectionUserDataTable");

        var collectionIdColumn = new DataColumn(nameof(collectionUser.CollectionId), collectionUser.CollectionId.GetType());
        table.Columns.Add(collectionIdColumn);
        var orgUserIdColumn = new DataColumn(nameof(collectionUser.OrganizationUserId), collectionUser.OrganizationUserId.GetType());
        table.Columns.Add(orgUserIdColumn);
        var readOnlyColumn = new DataColumn(nameof(collectionUser.ReadOnly), collectionUser.ReadOnly.GetType());
        table.Columns.Add(readOnlyColumn);
        var hidePasswordsColumn = new DataColumn(nameof(collectionUser.HidePasswords), collectionUser.HidePasswords.GetType());
        table.Columns.Add(hidePasswordsColumn);
        var manageColumn = new DataColumn(nameof(collectionUser.Manage), collectionUser.Manage.GetType());
        table.Columns.Add(manageColumn);

        foreach (DataColumn col in table.Columns)
        {
            bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        }

        var keys = new DataColumn[2];
        keys[0] = collectionIdColumn;
        keys[1] = orgUserIdColumn;
        table.PrimaryKey = keys;

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
        var collection = collections.FirstOrDefault();

        if (collection == null)
        {
            throw new ApplicationException(errorMessage);
        }

        var collectionsTable = new DataTable("CollectionDataTable");

        var idColumn = new DataColumn(nameof(collection.Id), collection.Id.GetType());
        collectionsTable.Columns.Add(idColumn);
        var organizationIdColumn = new DataColumn(nameof(collection.OrganizationId), collection.OrganizationId.GetType());
        collectionsTable.Columns.Add(organizationIdColumn);
        var nameColumn = new DataColumn(nameof(collection.Name), collection.Name.GetType());
        collectionsTable.Columns.Add(nameColumn);
        var creationDateColumn = new DataColumn(nameof(collection.CreationDate), collection.CreationDate.GetType());
        collectionsTable.Columns.Add(creationDateColumn);
        var revisionDateColumn = new DataColumn(nameof(collection.RevisionDate), collection.RevisionDate.GetType());
        collectionsTable.Columns.Add(revisionDateColumn);
        var externalIdColumn = new DataColumn(nameof(collection.ExternalId), typeof(string));
        collectionsTable.Columns.Add(externalIdColumn);
        var typeColumn = new DataColumn(nameof(collection.Type), collection.Type.GetType());
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

    private static DataTable BuildCiphersTable(SqlBulkCopy bulkCopy, IEnumerable<Cipher> ciphers, string errorMessage)
    {
        var c = ciphers.FirstOrDefault();

        if (c == null)
        {
            throw new ApplicationException(errorMessage);
        }

        var ciphersTable = new DataTable("CipherDataTable");

        var idColumn = new DataColumn(nameof(c.Id), c.Id.GetType());
        ciphersTable.Columns.Add(idColumn);
        var userIdColumn = new DataColumn(nameof(c.UserId), typeof(Guid));
        ciphersTable.Columns.Add(userIdColumn);
        var organizationId = new DataColumn(nameof(c.OrganizationId), typeof(Guid));
        ciphersTable.Columns.Add(organizationId);
        var typeColumn = new DataColumn(nameof(c.Type), typeof(short));
        ciphersTable.Columns.Add(typeColumn);
        var dataColumn = new DataColumn(nameof(c.Data), typeof(string));
        ciphersTable.Columns.Add(dataColumn);
        var favoritesColumn = new DataColumn(nameof(c.Favorites), typeof(string));
        ciphersTable.Columns.Add(favoritesColumn);
        var foldersColumn = new DataColumn(nameof(c.Folders), typeof(string));
        ciphersTable.Columns.Add(foldersColumn);
        var attachmentsColumn = new DataColumn(nameof(c.Attachments), typeof(string));
        ciphersTable.Columns.Add(attachmentsColumn);
        var creationDateColumn = new DataColumn(nameof(c.CreationDate), c.CreationDate.GetType());
        ciphersTable.Columns.Add(creationDateColumn);
        var revisionDateColumn = new DataColumn(nameof(c.RevisionDate), c.RevisionDate.GetType());
        ciphersTable.Columns.Add(revisionDateColumn);
        var deletedDateColumn = new DataColumn(nameof(c.DeletedDate), typeof(DateTime));
        ciphersTable.Columns.Add(deletedDateColumn);
        var repromptColumn = new DataColumn(nameof(c.Reprompt), typeof(short));
        ciphersTable.Columns.Add(repromptColumn);
        var keyColummn = new DataColumn(nameof(c.Key), typeof(string));
        ciphersTable.Columns.Add(keyColummn);

        foreach (DataColumn col in ciphersTable.Columns)
        {
            bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        }

        var keys = new DataColumn[1];
        keys[0] = idColumn;
        ciphersTable.PrimaryKey = keys;

        foreach (var cipher in ciphers)
        {
            var row = ciphersTable.NewRow();

            row[idColumn] = cipher.Id;
            row[userIdColumn] = cipher.UserId.HasValue ? (object)cipher.UserId.Value : DBNull.Value;
            row[organizationId] = cipher.OrganizationId.HasValue ? (object)cipher.OrganizationId.Value : DBNull.Value;
            row[typeColumn] = (short)cipher.Type;
            row[dataColumn] = cipher.Data;
            row[favoritesColumn] = cipher.Favorites;
            row[foldersColumn] = cipher.Folders;
            row[attachmentsColumn] = cipher.Attachments;
            row[creationDateColumn] = cipher.CreationDate;
            row[revisionDateColumn] = cipher.RevisionDate;
            row[deletedDateColumn] = cipher.DeletedDate.HasValue ? (object)cipher.DeletedDate : DBNull.Value;
            row[repromptColumn] = cipher.Reprompt.HasValue ? cipher.Reprompt.Value : DBNull.Value;
            row[keyColummn] = cipher.Key;

            ciphersTable.Rows.Add(row);
        }

        return ciphersTable;
    }

    private static DataTable BuildFoldersTable(SqlBulkCopy bulkCopy, IEnumerable<Folder> folders, string errorMessage)
    {
        var f = folders.FirstOrDefault();

        if (f == null)
        {
            throw new ApplicationException(errorMessage);
        }

        var foldersTable = new DataTable("FolderDataTable");

        var idColumn = new DataColumn(nameof(f.Id), f.Id.GetType());
        foldersTable.Columns.Add(idColumn);
        var userIdColumn = new DataColumn(nameof(f.UserId), f.UserId.GetType());
        foldersTable.Columns.Add(userIdColumn);
        var nameColumn = new DataColumn(nameof(f.Name), typeof(string));
        foldersTable.Columns.Add(nameColumn);
        var creationDateColumn = new DataColumn(nameof(f.CreationDate), f.CreationDate.GetType());
        foldersTable.Columns.Add(creationDateColumn);
        var revisionDateColumn = new DataColumn(nameof(f.RevisionDate), f.RevisionDate.GetType());
        foldersTable.Columns.Add(revisionDateColumn);

        foreach (DataColumn col in foldersTable.Columns)
        {
            bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        }

        var keys = new DataColumn[1];
        keys[0] = idColumn;
        foldersTable.PrimaryKey = keys;

        foreach (var folder in folders)
        {
            var row = foldersTable.NewRow();

            row[idColumn] = folder.Id;
            row[userIdColumn] = folder.UserId;
            row[nameColumn] = folder.Name;
            row[creationDateColumn] = folder.CreationDate;
            row[revisionDateColumn] = folder.RevisionDate;

            foldersTable.Rows.Add(row);
        }

        return foldersTable;
    }

    private static DataTable BuildCollectionCiphersTable(SqlBulkCopy bulkCopy, IEnumerable<CollectionCipher> collectionCiphers, string errorMessage)
    {
        var cc = collectionCiphers.FirstOrDefault();

        if (cc == null)
        {
            throw new ApplicationException(errorMessage);
        }

        var collectionCiphersTable = new DataTable("CollectionCipherDataTable");

        var collectionIdColumn = new DataColumn(nameof(cc.CollectionId), cc.CollectionId.GetType());
        collectionCiphersTable.Columns.Add(collectionIdColumn);
        var cipherIdColumn = new DataColumn(nameof(cc.CipherId), cc.CipherId.GetType());
        collectionCiphersTable.Columns.Add(cipherIdColumn);

        foreach (DataColumn col in collectionCiphersTable.Columns)
        {
            bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        }

        var keys = new DataColumn[2];
        keys[0] = collectionIdColumn;
        keys[1] = cipherIdColumn;
        collectionCiphersTable.PrimaryKey = keys;

        foreach (var collectionCipher in collectionCiphers)
        {
            var row = collectionCiphersTable.NewRow();

            row[collectionIdColumn] = collectionCipher.CollectionId;
            row[cipherIdColumn] = collectionCipher.CipherId;

            collectionCiphersTable.Rows.Add(row);
        }

        return collectionCiphersTable;
    }
}
