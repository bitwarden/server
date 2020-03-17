CREATE OR REPLACE PROCEDURE cipher_create(par_id uuid, par_user_id uuid, par_organization_id uuid, par_type numeric, par_data text, par_favorites text, par_folders text, par_attachments text, par_creation_date timestamp without time zone, par_revision_date timestamp without time zone)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    INSERT INTO cipher (id, user_id, organization_id, type, data, favorites, folders, attachments, creation_date, revision_date)
    VALUES (par_Id,
    CASE
        WHEN par_Organization_Id IS NULL THEN par_UserId
        ELSE NULL
    END, par_Organization_Id, par_Type, par_Data, par_Favorites, par_Folders, par_Attachments, par_Creation_Date, par_Revision_Date);

    IF par_Organization_Id IS NOT NULL THEN
        CALL user_bump_account_revision_date_by_cipher_id(par_Id, par_Organization_Id);
    ELSE
        IF par_User_Id IS NOT NULL THEN
            CALL user_bump_account_revision_date(par_User_Id);
        END IF;
    END IF;
END;
$procedure$
;
