CREATE PROCEDURE [dbo].[OrganizationUser_UpdateManySetStatus]
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
