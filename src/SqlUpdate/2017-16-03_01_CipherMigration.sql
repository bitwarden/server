-- Step 1, Run each statement individually

insert into folder
select Id, UserId, JSON_VALUE(Data,'$.Name') AS [Name], CreationDate, RevisionDate
from cipher
where [type] = 0

insert into foldercipher
select FolderId, Id, UserId
from cipher
where [FolderId] is not null

insert into favorite
select UserId, [Id]
from cipher
where Favorite = 1


-- Step 2, drop each column

alter table cipher drop constraint [FK_Cipher_Folder]
go

alter table cipher drop column FolderId
go

alter table cipher drop column Favorite
go

-- Step 3, delete old folder ciphers

delete from cipher where [type] = 0

