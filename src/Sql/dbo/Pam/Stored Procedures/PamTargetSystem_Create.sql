CREATE PROCEDURE [dbo].[PamTargetSystem_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(200),
    @Method TINYINT,
    @Kind TINYINT = NULL,
    @PasswordPolicy NVARCHAR(2000) = NULL,
    @SupportsSessionTermination BIT = NULL,
    @Status TINYINT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[PamTargetSystem]
    (
        [Id],
        [OrganizationId],
        [Name],
        [Method],
        [Kind],
        [PasswordPolicy],
        [SupportsSessionTermination],
        [Status],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Name,
        @Method,
        @Kind,
        @PasswordPolicy,
        @SupportsSessionTermination,
        @Status,
        @CreationDate,
        @RevisionDate
    )
END
