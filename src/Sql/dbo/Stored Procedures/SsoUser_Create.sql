CREATE PROCEDURE [dbo].[SsoUser_Create]
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ExternalId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[SsoUser]
    (
        [UserId],
        [OrganizationId],
        [ExternalId]
    )
    VALUES
    (
        @UserId,
        @OrganizationId,
        @ExternalId
    )
END