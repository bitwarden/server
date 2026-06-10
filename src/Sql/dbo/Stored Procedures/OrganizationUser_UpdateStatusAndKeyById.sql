CREATE PROCEDURE [dbo].[OrganizationUser_UpdateStatusAndKeyById]
    @Id UNIQUEIDENTIFIER,
    @Status SMALLINT,
    @Key VARCHAR(MAX),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationUser]
    SET
        [Status] = @Status,
        [Key] = @Key,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO
