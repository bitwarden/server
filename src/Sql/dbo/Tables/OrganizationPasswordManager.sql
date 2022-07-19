CREATE TABLE [dbo].[OrganizationPasswordManager](
    [Id] [uniqueidentifier] NOT NULL,
    [OrganizationId] [uniqueidentifier] NOT NULL,
    [Plan] [nvarchar](50) NOT NULL,
    [PlanType] [tinyint] NOT NULL,
    [Seats] [int] NULL,
    [MaxCollections] [smallint] NULL,
    [UseTotp] [bit] NULL,
    [UsersGetPremium] [bit] NOT NULL,
    [Storage] [bigint] NULL,
    [MaxStorageGb] [smallint] NULL,
    [MaxAutoscaleSeats] [int] NULL,
    [RevisionDate] DATETIME NULL,
    CONSTRAINT [PK_OrganizationPasswordManager] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationPasswordManager_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
)
