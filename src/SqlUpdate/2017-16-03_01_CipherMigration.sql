-- Step 1, Run each statement individually

insert into folder
select Id, UserId, JSON_VALUE(Data,'$.Name') AS [Name], CreationDate, RevisionDate
from cipher
where [type] = 0

update cipher set
Folders = concat('{"', userid, '":"', folderid, '"}')
where [userid] is not null
and [folderid] is not null

update cipher set
Favorites = concat('{"', userid, '":true}')
where [Favorite] = 1

-- Step 2, verify data migration from step 1 then drop each column

alter table cipher drop constraint [FK_Cipher_Folder]
go

alter table cipher drop column FolderId
go

alter table cipher drop column Favorite
go

-- Step 3, delete old folder ciphers

delete from cipher where [type] = 0

