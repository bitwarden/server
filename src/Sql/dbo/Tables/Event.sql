CREATE TABLE [dbo].[Event] (
    [Id]                     UNIQUEIDENTIFIER NOT NULL,
    [Type]                   INT              NOT NULL,
    [UserId]                 UNIQUEIDENTIFIER NULL,
    [OrganizationId]         UNIQUEIDENTIFIER NULL,
    [InstallationId]         UNIQUEIDENTIFIER NULL,
    [CipherId]               UNIQUEIDENTIFIER NULL,
    [CollectionId]           UNIQUEIDENTIFIER NULL,
    [PolicyId]               UNIQUEIDENTIFIER NULL,
    [GroupId]                UNIQUEIDENTIFIER NULL,
    [OrganizationUserId]     UNIQUEIDENTIFIER NULL,
    [ActingUserId]           UNIQUEIDENTIFIER NULL,
    [DeviceType]             SMALLINT         NULL,
    [IpAddress]              VARCHAR(50)      NULL,
    [Date]                   DATETIME2 (7)    NOT NULL,
    [ProviderId]             UNIQUEIDENTIFIER NULL,
    [ProviderUserId]         UNIQUEIDENTIFIER NULL,
    [ProviderOrganizationId] UNIQUEIDENTIFIER NULL,
    [SystemUser]             TINYINT          NULL,
    CONSTRAINT [PK_Event] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_Event_DateOrganizationIdUserId]
    ON [dbo].[Event]([Date] DESC, [OrganizationId] ASC, [ActingUserId] ASC, [CipherId] ASC);

