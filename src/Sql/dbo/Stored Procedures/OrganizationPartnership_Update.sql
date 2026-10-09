CREATE PROCEDURE [dbo].[OrganizationPartnership_Update]
    @Id                           UNIQUEIDENTIFIER,
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

    UPDATE
        [dbo].[OrganizationPartnership]
    SET
        [OrganizationId] = @OrganizationId,
        [Name] = @Name,
        [Status] = @Status,
        [SponsoredPlanType] = @SponsoredPlanType,
        [BindingMode] = @BindingMode,
        [IdentityBindingConfiguration] = @IdentityBindingConfiguration,
        [RegisteredReturnOrigins] = @RegisteredReturnOrigins,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
