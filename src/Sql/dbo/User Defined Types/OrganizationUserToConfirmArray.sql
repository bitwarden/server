CREATE TYPE [dbo].[OrganizationUserToConfirmArray] AS TABLE (
    [Id]     UNIQUEIDENTIFIER NOT NULL,
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    [Key]    NVARCHAR(MAX)    NULL);
GO
