IF COL_LENGTH('[dbo].[OrganizationApplication]', 'ContentEncryptionKey') IS NULL
BEGIN
ALTER TABLE [dbo].[OrganizationApplication]
    ADD [ContentEncryptionKey] VARCHAR(MAX) NOT NULL;
END
GO

CREATE OR ALTER VIEW [dbo].[OrganizationApplicationView]
AS
SELECT
    *
FROM
    [dbo].[OrganizationApplication]
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationApplication_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Applications NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @ContentEncryptionKey VARCHAR(MAX)
AS
    SET NOCOUNT ON;

    INSERT INTO [dbo].[OrganizationApplication]
    (
        [Id],
        [OrganizationId],
        [Applications],
        [CreationDate],
        [RevisionDate],
        [ContentEncryptionKey]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Applications,
        @CreationDate,
        @RevisionDate,
        @ContentEncryptionKey
    );
GO
