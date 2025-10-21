use std::{cmp::Ordering, collections::HashMap, sync::Arc};

use akd::{
    errors::StorageError,
    storage::{
        types::{self, DbRecord, KeyData, StorageType},
        Database, DbSetState, Storable,
    },
    AkdLabel, AkdValue,
};
use async_trait::async_trait;
use ms_database::{IntoRow, MsSqlConnectionManager, Pool, PooledConnection};

use crate::{
    migrations::MIGRATIONS,
    ms_sql_storable::{MsSqlStorable, Statement},
    tables::values,
    temp_table::TempTable,
};

const DEFAULT_POOL_SIZE: u32 = 100;

pub struct MsSqlBuilder {
    connection_string: String,
    pool_size: Option<u32>,
}

impl MsSqlBuilder {
    /// Create a new [MsSqlBuilder] with the given connection string.
    pub fn new(connection_string: String) -> Self {
        Self {
            connection_string,
            pool_size: None,
        }
    }

    /// Set the connection pool size. Default is given by [DEFAULT_POOL_SIZE].
    pub fn pool_size(mut self, pool_size: u32) -> Self {
        self.pool_size = Some(pool_size);
        self
    }

    /// Build the [MsSql] instance.
    pub async fn build(self) -> Result<MsSql, StorageError> {
        let pool_size = self.pool_size.unwrap_or(DEFAULT_POOL_SIZE);

        MsSql::new(self.connection_string, pool_size).await
    }
}

pub struct MsSql {
    pool: Arc<Pool>,
}

impl MsSql {
    pub fn builder(connection_string: String) -> MsSqlBuilder {
        MsSqlBuilder::new(connection_string)
    }

    pub async fn new(connection_string: String, pool_size: u32) -> Result<Self, StorageError> {
        let connection_manager = MsSqlConnectionManager::new(connection_string);
        let pool = Pool::builder()
            .max_size(pool_size)
            .build(connection_manager)
            .await
            .map_err(|e| StorageError::Connection(format!("Failed to create DB pool: {}", e)))?;

        Ok(Self {
            pool: Arc::new(pool),
        })
    }

    pub async fn migrate(&self) -> Result<(), StorageError> {
        let mut conn = self.pool.get().await.map_err(|e| {
            StorageError::Connection(format!("Failed to get DB connection for migrations: {}", e))
        })?;

        ms_database::run_pending_migrations(&mut conn, MIGRATIONS)
            .await
            .map_err(|e| StorageError::Connection(format!("Failed to run migrations: {}", e)))?;
        Ok(())
    }

    async fn get_connection(&self) -> Result<PooledConnection<'_>, StorageError> {
        self.pool
            .get()
            .await
            .map_err(|e| StorageError::Connection(format!("Failed to get DB connection: {}", e)))
    }

    async fn execute_statement(&self, statement: &Statement) -> Result<(), StorageError> {
        let mut conn = self.get_connection().await?;
        self.execute_statement_on_connection(statement, &mut conn)
            .await
    }

    async fn execute_statement_on_connection(
        &self,
        statement: &Statement,
        conn: &mut PooledConnection<'_>,
    ) -> Result<(), StorageError> {
        conn.execute(statement.sql(), &statement.params())
            .await
            .map_err(|e| StorageError::Other(format!("Failed to execute statement: {}", e)))?;
        Ok(())
    }
}

#[async_trait]
impl Database for MsSql {
    async fn set(&self, record: DbRecord) -> Result<(), StorageError> {
        let statement = record.set_statement()?;
        self.execute_statement(&statement).await
    }

    async fn batch_set(
        &self,
        records: Vec<DbRecord>,
        _state: DbSetState, // TODO: unused in mysql example, but may be needed later
    ) -> Result<(), StorageError> {
        // Generate groups by type
        let mut groups = HashMap::new();
        for record in records {
            match &record {
                DbRecord::Azks(_) => groups
                    .entry(StorageType::Azks)
                    .or_insert_with(Vec::new)
                    .push(record),
                DbRecord::TreeNode(_) => groups
                    .entry(StorageType::TreeNode)
                    .or_insert_with(Vec::new)
                    .push(record),
                DbRecord::ValueState(_) => groups
                    .entry(StorageType::ValueState)
                    .or_insert_with(Vec::new)
                    .push(record),
            }
        }

        // Execute each group in batches
        let mut conn = self.get_connection().await?;
        // Start transaction
        conn.simple_query("BEGIN TRANSACTION")
            .await
            .map_err(|e| StorageError::Transaction(format!("Failed to begin transaction: {e}")))?;
        let result = async {
            for (storage_type, mut record_group) in groups.into_iter() {
                if record_group.is_empty() {
                    continue;
                }

                // Sort the records to match db-layer sorting
                record_group.sort_by(|a, b| match &a {
                    DbRecord::TreeNode(node) => {
                        if let DbRecord::TreeNode(node2) = &b {
                            node.label.cmp(&node2.label)
                        } else {
                            Ordering::Equal
                        }
                    }
                    DbRecord::ValueState(state) => {
                        if let DbRecord::ValueState(state2) = &b {
                            match state.username.0.cmp(&state2.username.0) {
                                Ordering::Equal => state.epoch.cmp(&state2.epoch),
                                other => other,
                            }
                        } else {
                            Ordering::Equal
                        }
                    }
                    _ => Ordering::Equal,
                });

                // Execute value as bulk insert

                // Create temp table
                let table: TempTable = storage_type.into();
                conn.simple_query(&table.create()).await.map_err(|e| {
                    StorageError::Other(format!("Failed to create temp table: {e}"))
                })?;

                // Create bulk insert
                let table_name = &table.to_string();
                let mut bulk = conn.bulk_insert(table_name).await.map_err(|e| {
                    StorageError::Other(format!("Failed to start bulk insert: {e}"))
                })?;

                for record in &record_group {
                    let row = record.into_row()?;
                    bulk.send(row).await.map_err(|e| {
                        StorageError::Other(format!("Failed to add row to bulk insert: {e}"))
                    })?;
                }

                bulk.finalize().await.map_err(|e| {
                    StorageError::Other(format!("Failed to finalize bulk insert: {e}"))
                })?;

                // Set values from temp table to main table
                let sql = <DbRecord as MsSqlStorable>::set_batch_statement(&storage_type);
                conn.simple_query(&sql).await.map_err(|e| {
                    StorageError::Other(format!("Failed to execute batch set statement: {e}"))
                })?;

                // Delete the temp table
                conn.simple_query(&table.drop())
                    .await
                    .map_err(|e| StorageError::Other(format!("Failed to drop temp table: {e}")))?;
            }

            Ok::<(), StorageError>(())
        };

        match result.await {
            Ok(_) => {
                conn.simple_query("COMMIT").await.map_err(|e| {
                    StorageError::Transaction(format!("Failed to commit transaction: {e}"))
                })?;
                Ok(())
            }
            Err(e) => {
                conn.simple_query("ROLLBACK").await.map_err(|e| {
                    StorageError::Transaction(format!("Failed to roll back transaction: {e}"))
                })?;
                Err(StorageError::Other(format!(
                    "Failed to batch set records: {}",
                    e
                )))
            }
        }
    }

    async fn get<St: Storable>(&self, id: &St::StorageKey) -> Result<DbRecord, StorageError> {
        let mut conn = self.get_connection().await?;
        let statement = DbRecord::get_statement::<St>(id)?;

        let query_stream = conn
            .query(statement.sql(), &statement.params())
            .await
            .map_err(|e| StorageError::Other(format!("Failed to execute query: {e}")))?;

        let row = query_stream
            .into_row()
            .await
            .map_err(|e| StorageError::Other(format!("Failed to fetch row: {e}")))?;

        if let Some(row) = row {
            DbRecord::from_row::<St>(&row)
        } else {
            Err(StorageError::NotFound(format!(
                "{:?} {:?} not found",
                St::data_type(),
                id
            )))
        }
    }

    async fn batch_get<St: Storable>(
        &self,
        ids: &[St::StorageKey],
    ) -> Result<Vec<DbRecord>, StorageError> {
        if ids.is_empty() {
            return Ok(vec![]);
        }

        let temp_table = TempTable::for_ids::<St>();
        if temp_table.can_create() {
            // AZKs does not support batch get, so we just return empty vec
            return Ok(Vec::new());
        }
        let create_temp_table = temp_table.create();
        let temp_table_name = &temp_table.to_string();

        let mut conn = self.get_connection().await?;

        // Begin a transaction
        conn.simple_query("BEGIN TRANSACTION")
            .await
            .map_err(|e| StorageError::Transaction(format!("Failed to begin transaction: {e}")))?;

        let result = async {
            // Use bulk_insert to insert all the ids into a temporary table
            conn.simple_query(&create_temp_table)
                .await
                .map_err(|e| StorageError::Other(format!("Failed to create temp table: {e}")))?;
            let mut bulk = conn
                .bulk_insert(&temp_table_name)
                .await
                .map_err(|e| StorageError::Other(format!("Failed to start bulk insert: {e}")))?;
            for row in DbRecord::get_batch_temp_table_rows::<St>(ids)? {
                bulk.send(row).await.map_err(|e| {
                    StorageError::Other(format!("Failed to add row to bulk insert: {e}"))
                })?;
            }
            bulk.finalize()
                .await
                .map_err(|e| StorageError::Other(format!("Failed to finalize bulk insert: {e}")))?;

            // Read rows matching the ids from the temporary table
            let get_sql = DbRecord::get_batch_statement::<St>();
            let query_stream = conn.simple_query(&get_sql).await.map_err(|e| {
                StorageError::Other(format!("Failed to execute batch get query: {e}"))
            })?;
            let mut records = Vec::new();
            {
                let rows = query_stream
                    .into_first_result()
                    .await
                    .map_err(|e| StorageError::Other(format!("Failed to fetch rows: {e}")))?;
                for row in rows {
                    let record = DbRecord::from_row::<St>(&row)?;
                    records.push(record);
                }
            }

            let drop_temp_table = temp_table.drop();
            conn.simple_query(&drop_temp_table)
                .await
                .map_err(|e| StorageError::Other(format!("Failed to drop temp table: {e}")))?;

            Ok::<Vec<DbRecord>, StorageError>(vec![])
        };

        // Commit or rollback the transaction
        match result.await {
            Ok(records) => {
                conn.simple_query("COMMIT").await.map_err(|e| {
                    StorageError::Transaction(format!("Failed to commit transaction: {e}"))
                })?;
                Ok(records)
            }
            Err(e) => {
                conn.simple_query("ROLLBACK").await.map_err(|e| {
                    StorageError::Transaction(format!("Failed to rollback transaction: {e}"))
                })?;
                Err(e)
            }
        }
    }

    // Note: user and username here is the raw_label. The assumption is this is a single key for a single user, but that's
    // too restrictive for what we want, so generalize the name a bit.
    async fn get_user_data(&self, raw_label: &AkdLabel) -> Result<types::KeyData, StorageError> {
        // Note: don't log raw_label or data as it may contain sensitive information, such as PII.

        let result = async {
            let mut conn = self.get_connection().await?;

            let statement = values::get_all(raw_label);

            let query_stream = conn
                .query(statement.sql(), &statement.params())
                .await
                .map_err(|e| {
                    StorageError::Other(format!("Failed to execute query for label: {e}"))
                })?;
            let mut states = Vec::new();
            {
                let rows = query_stream.into_first_result().await.map_err(|e| {
                    StorageError::Other(format!("Failed to fetch rows for label: {e}"))
                })?;
                for row in rows {
                    let record = statement.parse(&row)?;
                    states.push(record);
                }
            }
            let key_data = KeyData { states };

            Ok::<KeyData, StorageError>(key_data)
        };

        match result.await {
            Ok(data) => Ok(data),
            Err(e) => Err(StorageError::Other(format!(
                "Failed to get all data for label: {}",
                e
            ))),
        }
    }

    // Note: user and username here is the raw_label. The assumption is this is a single key for a single user, but that's
    // too restrictive for what we want, so generalize the name a bit.
    async fn get_user_state(
        &self,
        raw_label: &AkdLabel,
        flag: types::ValueStateRetrievalFlag,
    ) -> Result<types::ValueState, StorageError> {
        let statement = values::get_by_flag(raw_label, flag);
        let mut conn = self.get_connection().await?;
        let query_stream = conn
            .query(statement.sql(), &statement.params())
            .await
            .map_err(|e| StorageError::Other(format!("Failed to execute query: {e}")))?;
        let row = query_stream
            .into_row()
            .await
            .map_err(|e| StorageError::Other(format!("Failed to fetch first result row: {e}")))?;
        if let Some(row) = row {
            statement.parse(&row)
        } else {
            Err(StorageError::NotFound(format!(
                "ValueState for label {:?} not found",
                raw_label
            )))
        }
    }

    // Note: user and username here is the raw_label. The assumption is this is a single key for a single user, but that's
    // too restrictive for what we want, so generalize the name a bit.
    async fn get_user_state_versions(
        &self,
        raw_labels: &[AkdLabel],
        flag: types::ValueStateRetrievalFlag,
    ) -> Result<HashMap<AkdLabel, (u64, AkdValue)>, StorageError> {
        let mut conn = self.get_connection().await?;

        let temp_table = TempTable::RawLabelSearch;
        let create_temp_table = temp_table.create();
        let temp_table_name = &temp_table.to_string();

        // Begin a transaction
        conn.simple_query("BEGIN TRANSACTION")
            .await
            .map_err(|e| StorageError::Transaction(format!("Failed to begin transaction: {e}")))?;

        let result = async {
            conn.simple_query(&create_temp_table)
                .await
                .map_err(|e| StorageError::Other(format!("Failed to create temp table: {e}")))?;

            // Use bulk_insert to insert all the raw_labels into a temporary table
            let mut bulk = conn
                .bulk_insert(&temp_table_name)
                .await
                .map_err(|e| StorageError::Other(format!("Failed to start bulk insert: {e}")))?;
            for raw_label in raw_labels {
                let row = (raw_label.0.clone()).into_row();
                bulk.send(row).await.map_err(|e| {
                    StorageError::Other(format!("Failed to add row to bulk insert: {e}"))
                })?;
            }
            bulk.finalize()
                .await
                .map_err(|e| StorageError::Other(format!("Failed to finalize bulk insert: {e}")))?;

            // read rows matching the raw_labels from the temporary table
            let statement = values::get_versions_by_flag(&temp_table_name, flag);
            let query_stream = conn
                .query(statement.sql(), &statement.params())
                .await
                .map_err(|e| {
                    StorageError::Other(format!("Failed to execute batch get query: {e}"))
                })?;

            let mut results = HashMap::new();
            let rows = query_stream
                .into_first_result()
                .await
                .map_err(|e| StorageError::Other(format!("Failed to fetch rows: {e}")))?;
            for row in rows {
                let label_version = statement.parse(&row)?;
                results.insert(
                    label_version.label,
                    (label_version.version, label_version.data),
                );
            }
            Ok(results)
        };

        match result.await {
            Ok(results) => {
                conn.simple_query("COMMIT").await.map_err(|e| {
                    StorageError::Transaction(format!("Failed to commit transaction: {e}"))
                })?;
                Ok(results)
            }
            Err(e) => {
                conn.simple_query("ROLLBACK").await.map_err(|e| {
                    StorageError::Transaction(format!("Failed to rollback transaction: {e}"))
                })?;
                Err(e)
            }
        }
    }
}
