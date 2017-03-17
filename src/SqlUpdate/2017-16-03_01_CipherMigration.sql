insert into folder
select Id, UserId, JSON_VALUE(Data,'$.Name') AS [Name], CreationDate, RevisionDate
from cipher
where [type] = 0

insert into foldercipher
select FolderId, Id
from cipher
where [FolderId] is not null

insert into favorite
select UserId, [Id]
from cipher
where Favorite = 1
