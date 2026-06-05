CREATE OR ALTER PROCEDURE [dbo].[EmergencyAccess_UpdateManySetStatus]
    @Ids [dbo].[GuidIdArray] READONLY,
    @Status TINYINT,
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        EA
    SET
        EA.[Status] = @Status,
        EA.[KeyEncrypted] = NULL,
        EA.[RevisionDate] = @RevisionDate
    FROM
        [dbo].[EmergencyAccess] EA
    INNER JOIN
        @Ids I ON I.[Id] = EA.[Id]
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_UpdateManySetStatus]
    @OrganizationUserIds [dbo].[GuidIdArray] READONLY,
    @Status SMALLINT,
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        OU
    SET
        OU.[Status] = @Status,
        OU.[Key] = NULL,
        OU.[RevisionDate] = @RevisionDate
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        @OrganizationUserIds OUI ON OUI.[Id] = OU.[Id]
END
GO
