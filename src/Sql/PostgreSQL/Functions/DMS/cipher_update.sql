CREATE OR REPLACE PROCEDURE cipher_update(par_id uuid, par_userid uuid, par_organizationid uuid, par_type numeric, par_data text, par_favorites text, par_folders text, par_attachments text, par_creationdate timestamp without time zone, par_revisiondate timestamp without time zone)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    UPDATE cipher
    SET userid =
    CASE
        WHEN par_OrganizationId IS NULL THEN par_UserId
        ELSE NULL
    END, organizationid = par_OrganizationId, type = par_Type, data = par_Data, favorites = par_Favorites, folders = par_Folders, attachments = par_Attachments, creationdate = par_CreationDate, revisiondate = par_RevisionDate
        WHERE id = par_Id;

    IF par_OrganizationId IS NOT NULL THEN
        CALL user_bumpaccountrevisiondatebycipherid(par_Id, par_OrganizationId);
    ELSE
        IF par_UserId IS NOT NULL THEN
            CALL user_bumpaccountrevisiondate(par_UserId);
        END IF;
    END IF;
END;
$procedure$
;
