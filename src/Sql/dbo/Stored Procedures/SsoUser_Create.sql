CREATE PROCEDURE [dbo].[SsoUser_Create]
    @Id BIGINT OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ExternalId NVARCHAR(50),
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[SsoUser]
    (
        [UserId],
        [OrganizationId],
        [ExternalId],
        [CreationDate]
    )
    VALUES
    (
        @UserId,
        @OrganizationId,
        @ExternalId,
        @CreationDate
    )

    SET @Id = SCOPE_IDENTITY();
END