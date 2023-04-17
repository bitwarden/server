CREATE PROCEDURE [dbo].[SsoUser_Update]
    @Id BIGINT OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ExternalId NVARCHAR(50),
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[SsoUser]
    SET
        [UserId] = @UserId,
        [OrganizationId] = @OrganizationId,
        [ExternalId] = @ExternalId,
        [CreationDate] = @CreationDate
    WHERE
        [Id] = @Id
END