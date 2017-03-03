CREATE PROCEDURE [dbo].[Organization_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @Plan NVARCHAR(20),
    @PlanType TINYINT,
    @PlanPrice MONEY,
    @PlanRenewalPrice MONEY,
    @PlanRenewalDate DATETIME2(7),
    @PlanTrial BIT,
    @MaxUsers SMALLINT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Organization]
    SET
        [UserId] = @UserId,
        [Name] = @Name,
        [Plan] = @Plan,
        [PlanType] = @PlanType,
        [PlanPrice] = @PlanPrice,
        [PlanRenewalPrice] = @PlanRenewalPrice,
        [PlanRenewalDate] = @PlanRenewalDate,
        [PlanTrial] = @PlanTrial,
        [MaxUsers] = @MaxUsers,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END