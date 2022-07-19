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
    [RevisionDate] DATETIME NULL,
    CONSTRAINT [PK_OrganizationSecretsManager] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationSecretsManager_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
) 
