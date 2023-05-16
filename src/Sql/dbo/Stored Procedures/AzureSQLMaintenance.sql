-- ref: https://blogs.msdn.microsoft.com/azuresqldbsupport/2016/07/03/how-to-maintain-azure-sql-indexes-and-statistics/

CREATE Procedure [dbo].[AzureSQLMaintenance]
    (
        @operation nvarchar(10) = null,
        @mode nvarchar(10) = 'smart',
        @LogToTable bit = 0
    )
as
begin
    set nocount on
    declare @msg nvarchar(max);
    declare @minPageCountForIndex int = 40;
    declare @OperationTime datetime2 = sysdatetime();
    declare @KeepXOperationInLog int =3;

    /* make sure parameters selected correctly */
    set @operation = lower(@operation)
    set @mode = lower(@mode)
    
    if @mode not in ('smart','dummy')
        set @mode = 'smart'

    if @operation not in ('index','statistics','all') or @operation is null
    begin
        raiserror('@operation (varchar(10)) [mandatory]',0,0)
        raiserror(' Select operation to perform:',0,0)
        raiserror('     "index" to perform index maintenance',0,0)
        raiserror('     "statistics" to perform statistics maintenance',0,0)
        raiserror('     "all" to perform indexes and statistics maintenance',0,0)
        raiserror(' ',0,0)
        raiserror('@mode(varchar(10)) [optional]',0,0)
        raiserror(' optionaly you can supply second parameter for operation mode: ',0,0)
        raiserror('     "smart" (Default) using smart decition about what index or stats should be touched.',0,0)
        raiserror('     "dummy" going through all indexes and statistics regardless thier modifications or fragmentation.',0,0)
        raiserror(' ',0,0)
        raiserror('@LogToTable(bit) [optional]',0,0)
        raiserror(' Logging option: @LogToTable(bit)',0,0)
        raiserror('     0 - (Default) do not log operation to table',0,0)
        raiserror('     1 - log operation to table',0,0)
        raiserror('		for logging option only 3 last execution will be kept by default. this can be changed by easily in the procedure body.',0,0)
        raiserror('		Log table will be created automatically if not exists.',0,0)
    end
    else 
    begin
        /*Write operation parameters*/
        raiserror('-----------------------',0,0)
        set @msg = 'set operation = ' + @operation;
        raiserror(@msg,0,0)
        set @msg = 'set mode = ' + @mode;
        raiserror(@msg,0,0)
        set @msg = 'set LogToTable = ' + cast(@LogToTable as varchar(1));
        raiserror(@msg,0,0)
        raiserror('-----------------------',0,0)
    end
    
    /* Prepare Log Table */
        if object_id('AzureSQLMaintenanceLog') is null 
        begin
            create table AzureSQLMaintenanceLog (id bigint primary key identity(1,1), OperationTime datetime2, command varchar(4000),ExtraInfo varchar(4000), StartTime datetime2, EndTime datetime2, StatusMessage varchar(1000));
        end

    if @LogToTable=1 insert into AzureSQLMaintenanceLog values(@OperationTime,null,null,sysdatetime(),sysdatetime(),'Starting operation: Operation=' +@operation + ' Mode=' + @mode + ' Keep log for last ' + cast(@KeepXOperationInLog as varchar(10)) + ' operations' )	

    create table #cmdQueue (txtCMD nvarchar(max),ExtraInfo varchar(max))


    if @operation in('index','all')
    begin
        raiserror('Get index information...(wait)',0,0) with nowait;
        /* Get Index Information */
        select 
            i.[object_id]
            ,ObjectSchema = OBJECT_SCHEMA_NAME(i.object_id)
            ,ObjectName = object_name(i.object_id) 
            ,IndexName = idxs.name
            ,i.avg_fragmentation_in_percent
            ,i.page_count
            ,i.index_id
            ,i.partition_number
            ,i.index_type_desc
            ,i.avg_page_space_used_in_percent
            ,i.record_count
            ,i.ghost_record_count
            ,i.forwarded_record_count
            ,null as OnlineOpIsNotSupported
        into #idxBefore
        from sys.dm_db_index_physical_stats(DB_ID(),NULL, NULL, NULL ,'limited') i
        left join sys.indexes idxs on i.object_id = idxs.object_id and i.index_id = idxs.index_id
        where idxs.type in (1/*Clustered index*/,2/*NonClustered index*/) /*Avoid HEAPS*/
        order by i.avg_fragmentation_in_percent desc, page_count desc


        -- mark indexes XML,spatial and columnstore not to run online update 
        update #idxBefore set OnlineOpIsNotSupported=1 where [object_id] in (select [object_id] from #idxBefore where index_id >=1000)
        
        
        raiserror('---------------------------------------',0,0) with nowait
        raiserror('Index Information:',0,0) with nowait
        raiserror('---------------------------------------',0,0) with nowait

        select @msg = count(*) from #idxBefore where index_id in (1,2)
        set @msg = 'Total Indexes: ' + @msg
        raiserror(@msg,0,0) with nowait

        select @msg = avg(avg_fragmentation_in_percent) from #idxBefore where index_id in (1,2) and page_count>@minPageCountForIndex
        set @msg = 'Average Fragmentation: ' + @msg
        raiserror(@msg,0,0) with nowait

        select @msg = sum(iif(avg_fragmentation_in_percent>=5 and page_count>@minPageCountForIndex,1,0)) from #idxBefore where index_id in (1,2)
        set @msg = 'Fragmented Indexes: ' + @msg
        raiserror(@msg,0,0) with nowait

                
        raiserror('---------------------------------------',0,0) with nowait

            
            
            
        /* create queue for update indexes */
        insert into #cmdQueue
        select 
        txtCMD = 
        case when avg_fragmentation_in_percent>5 and avg_fragmentation_in_percent<30 and @mode = 'smart' then
            'ALTER INDEX [' + IndexName + '] ON [' + ObjectSchema + '].[' + ObjectName + '] REORGANIZE;'
            when OnlineOpIsNotSupported=1 then
            'ALTER INDEX [' + IndexName + '] ON [' + ObjectSchema + '].[' + ObjectName + '] REBUILD WITH(ONLINE=OFF,MAXDOP=1);'
            else
            'ALTER INDEX [' + IndexName + '] ON [' + ObjectSchema + '].[' + ObjectName + '] REBUILD WITH(ONLINE=ON,MAXDOP=1);'
        end
        , ExtraInfo = 'Current fragmentation: ' + format(avg_fragmentation_in_percent/100,'p')
        from #idxBefore
        where 
            index_id>0 /*disable heaps*/ 
            and index_id < 1000 /* disable XML indexes */
            --
            and 
                (
                    page_count> @minPageCountForIndex and /* not small tables */
                    avg_fragmentation_in_percent>=5
                )
            or
                (
                    @mode ='dummy'
                )
    end

    if @operation in('statistics','all')
    begin 
        /*Gets Stats for database*/
        raiserror('Get statistics information...',0,0) with nowait;
        select 
            ObjectSchema = OBJECT_SCHEMA_NAME(s.object_id)
            ,ObjectName = object_name(s.object_id) 
            ,StatsName = s.name
            ,sp.last_updated
            ,sp.rows
            ,sp.rows_sampled
            ,sp.modification_counter
        into #statsBefore
        from sys.stats s cross apply sys.dm_db_stats_properties(s.object_id,s.stats_id) sp 
        where OBJECT_SCHEMA_NAME(s.object_id) != 'sys' and (sp.modification_counter>0 or @mode='dummy')
        order by sp.last_updated asc

        
        raiserror('---------------------------------------',0,0) with nowait
        raiserror('Statistics Information:',0,0) with nowait
        raiserror('---------------------------------------',0,0) with nowait

        select @msg = sum(modification_counter) from #statsBefore
        set @msg = 'Total Modifications: ' + @msg
        raiserror(@msg,0,0) with nowait
        
        select @msg = sum(iif(modification_counter>0,1,0)) from #statsBefore
        set @msg = 'Modified Statistics: ' + @msg
        raiserror(@msg,0,0) with nowait
                
        raiserror('---------------------------------------',0,0) with nowait




        /* create queue for update stats */
        insert into #cmdQueue
        select 
        txtCMD = 'UPDATE STATISTICS [' + ObjectSchema + '].[' + ObjectName + '] (['+ StatsName +']) WITH FULLSCAN;'
        , ExtraInfo = '#rows:' + cast([rows] as varchar(100)) + ' #modifications:' + cast(modification_counter as varchar(100)) + ' modification percent: ' + format((1.0 * modification_counter/ rows ),'p')
        from #statsBefore
    end


    if @operation in('statistics','index','all')
    begin 
        /* iterate through all stats */
        raiserror('Start executing commands...',0,0) with nowait
        declare @SQLCMD nvarchar(max);
        declare @ExtraInfo nvarchar(max);
        declare @T table(txtCMD nvarchar(max),ExtraInfo nvarchar(max));
        while exists(select * from #cmdQueue)
        begin
            delete top (1) from #cmdQueue output deleted.* into @T;
            select top (1) @SQLCMD = txtCMD, @ExtraInfo=ExtraInfo from @T
            raiserror(@SQLCMD,0,0) with nowait
            if @LogToTable=1 insert into AzureSQLMaintenanceLog values(@OperationTime,@SQLCMD,@ExtraInfo,sysdatetime(),null,'Started')
            begin try
                exec(@SQLCMD)	
                if @LogToTable=1 update AzureSQLMaintenanceLog set EndTime = sysdatetime(), StatusMessage = 'Succeeded' where id=SCOPE_IDENTITY()
            end try
            begin catch
                raiserror('cached',0,0) with nowait
                if @LogToTable=1 update AzureSQLMaintenanceLog set EndTime = sysdatetime(), StatusMessage = 'FAILED : ' + CAST(ERROR_NUMBER() AS VARCHAR(50)) + ERROR_MESSAGE() where id=SCOPE_IDENTITY()
            end catch
            delete from @T
        end
    end
    
    /* Clean old records from log table */
    if @LogToTable=1
    begin
        delete from AzureSQLMaintenanceLog 
        from 
            AzureSQLMaintenanceLog L join 
            (select distinct OperationTime from AzureSQLMaintenanceLog order by OperationTime desc offset @KeepXOperationInLog rows) F
                ON L.OperationTime = F.OperationTime
        insert into AzureSQLMaintenanceLog values(@OperationTime,null,cast(@@rowcount as varchar(100))+ ' rows purged from log table because number of operations to keep is set to: ' + cast( @KeepXOperationInLog as varchar(100)),sysdatetime(),sysdatetime(),'Cleanup Log Table')
    end

    raiserror('Done',0,0)
    if @LogToTable=1 insert into AzureSQLMaintenanceLog values(@OperationTime,null,null,sysdatetime(),sysdatetime(),'End of operation')
end
