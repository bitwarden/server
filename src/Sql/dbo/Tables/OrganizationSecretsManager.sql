CREATE TABLE [dbo].[OrganizationSecretsManager](
    [Id] [uniqueidentifier] NOT NULL,
    [OrganizationId] [uniqueidentifier] NOT NULL,
    [Plan] [nvarchar](50) NOT NULL,
    [PlanType] [tinyint] NOT NULL,
    [UserSeats] [int] NULL,
    [ServiceAccountSeats] [int] NULL,
    [UseEnvironments] [bit] NULL,
    [MaxAutoscaleUserSeats] [int] NULL,
    [MaxAutoscaleServiceAccounts] [int] NULL,
    [MaxProjects] [int] NULL,
    [RevisionDate] DATETIME NULL
) 