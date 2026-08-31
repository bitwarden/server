CREATE PROCEDURE [dbo].[PamTargetSystem_Update]
    @Id UNIQUEIDENTIFIER,
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

    UPDATE
        [dbo].[PamTargetSystem]
    SET
        [OrganizationId] = @OrganizationId,
        [Name] = @Name,
        [Method] = @Method,
        [Kind] = @Kind,
        [PasswordPolicy] = @PasswordPolicy,
        [SupportsSessionTermination] = @SupportsSessionTermination,
        [Status] = @Status,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
