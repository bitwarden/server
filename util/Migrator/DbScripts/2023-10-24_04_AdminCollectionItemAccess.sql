--Add column 'AllowAdminAccessToAllCollectionItems' to 'Organization' table
IF COL_LENGTH('[dbo].[Organization]', 'AllowAdminAccessToAllCollectionItems') IS NULL
BEGIN
ALTER TABLE
    [dbo].[Organization]
    ADD
    [AllowAdminAccessToAllCollectionItems] BIT NOT NULL CONSTRAINT [DF_Organization_AllowAdminAccessToAllCollectionItems] DEFAULT (1)
END
GO
