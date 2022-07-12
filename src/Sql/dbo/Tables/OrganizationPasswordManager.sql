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
    [RevisionDate] DATETIME NULL
) 