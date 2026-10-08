CREATE PROCEDURE [dbo].[OrganizationPartnership_Create]
    @Id                           UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId               UNIQUEIDENTIFIER,
    @Name                         NVARCHAR(50),
    @Status                       TINYINT,
    @SponsoredPlanType            TINYINT,
    @BindingMode                  TINYINT,
    @IdentityBindingConfiguration NVARCHAR(MAX),
    @RegisteredReturnOrigins      NVARCHAR(MAX),
    @CreationDate                 DATETIME2(7),
    @RevisionDate                 DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationPartnership]
    (
        [Id],
        [OrganizationId],
        [Name],
        [Status],
        [SponsoredPlanType],
        [BindingMode],
        [IdentityBindingConfiguration],
        [RegisteredReturnOrigins],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Name,
        @Status,
        @SponsoredPlanType,
        @BindingMode,
        @IdentityBindingConfiguration,
        @RegisteredReturnOrigins,
        @CreationDate,
        @RevisionDate
    )
END
