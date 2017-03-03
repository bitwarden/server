CREATE PROCEDURE [dbo].[Organization_Create]
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

    INSERT INTO [dbo].[Organization]
    (
        [Id],
        [UserId],
        [Name],
        [Plan],
        [PlanType],
        [PlanPrice],
        [PlanRenewalPrice],
        [PlanRenewalDate],
        [PlanTrial],
        [MaxUsers],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @Name,
        @Plan,
        @PlanType,
        @PlanPrice,
        @PlanRenewalPrice,
        @PlanRenewalDate,
        @PlanTrial,
        @MaxUsers,
        @CreationDate,
        @RevisionDate
    )
END