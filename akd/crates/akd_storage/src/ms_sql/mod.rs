pub(crate) mod migrations;
pub(crate) mod sql_params;
pub(crate) mod tables;

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
use tracing::{debug, error, info, instrument, trace, warn};

use migrations::{
    MIGRATIONS, TABLE_AZKS, TABLE_HISTORY_TREE_NODES, TABLE_MIGRATIONS, TABLE_VALUES,
    TABLE_VRF_KEYS,
};
use tables::{
    akd_storable_for_ms_sql::{AkdStorableForMsSql, Statement},
    temp_table::TempTable,
    values,
};

use crate::{
    ms_sql::tables::vrf_key,
    vrf_key_config::VrfKeyConfig,
    vrf_key_database::{VrfKeyRetrievalError, VrfKeyStorageError, VrfKeyTableData},
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

#[derive(Debug, Clone)]
pub struct MsSql {
    pool: Arc<Pool>,
}

impl MsSql {
    pub fn builder(connection_string: String) -> MsSqlBuilder {
        MsSqlBuilder::new(connection_string)
    }

    #[instrument(skip(connection_string), fields(pool_size))]
    pub async fn new(connection_string: String, pool_size: u32) -> Result<Self, StorageError> {
        info!(pool_size, "Creating MS SQL storage");
        let connection_manager = MsSqlConnectionManager::new(connection_string);
        let pool = Pool::builder()
            .max_size(pool_size)
            .build(connection_manager)
            .await
            .map_err(|e| {
                error!(error = %e, "Failed to create DB pool");
                StorageError::Connection(format!("Failed to create DB pool: {e}"))
            })?;

        info!("Successfully created MS SQL storage connection pool");
        Ok(Self {
            pool: Arc::new(pool),
        })
    }

    #[instrument(skip(self))]
    pub async fn migrate(&self) -> Result<(), StorageError> {
        info!("Running database migrations");
        let mut conn = self.pool.get().await.map_err(|e| {
            error!(error = %e, "Failed to get DB connection for migrations");
            StorageError::Connection(format!("Failed to get DB connection for migrations: {e}"))
        })?;

        ms_database::run_pending_migrations(&mut conn, MIGRATIONS)
            .await
            .map_err(|e| {
                error!(error = %e, "Failed to run migrations");
                StorageError::Connection(format!("Failed to run migrations: {e}"))
            })?;
        info!("Successfully completed database migrations");
        Ok(())
    }

    pub async fn drop(&self) -> Result<(), StorageError> {
        info!("Dropping all AKD tables");
        let mut conn = self.pool.get().await.map_err(|e| {
            error!(error = %e, "Failed to get DB connection for dropping tables");
            StorageError::Connection(format!(
                "Failed to get DB connection for dropping tables: {e}"
            ))
        })?;

        let drop_all = format!(
            r#"
            DROP TABLE IF EXISTS {TABLE_AZKS};
            DROP TABLE IF EXISTS {TABLE_HISTORY_TREE_NODES};
            DROP TABLE IF EXISTS {TABLE_VALUES};
            DROP TABLE IF EXISTS {TABLE_VRF_KEYS};
            DROP TABLE IF EXISTS {TABLE_MIGRATIONS};"#
        );

        conn.simple_query(&drop_all).await.map_err(|e| {
            error!(error = ?e, sql = drop_all, "Failed to execute drop for all tables");
            StorageError::Other(format!("Failed to drop AKD tables: {e}"))
        })?;

        info!("Successfully dropped all AKD tables");
        Ok(())
    }

    #[instrument(skip(self), level = "trace")]
    async fn get_connection(&self) -> Result<PooledConnection<'_>, StorageError> {
        trace!("Acquiring database connection from pool");
        self.pool.get().await.map_err(|e| {
            error!(error = %e, "Failed to get DB connection");
            StorageError::Connection(format!("Failed to get DB connection: {e}"))
        })
    }

    #[instrument(skip(self, statement), level = "debug")]
    async fn execute_statement(&self, statement: &Statement) -> Result<(), StorageError> {
        debug!("Executing SQL statement");
        trace!(sql = statement.sql(), "SQL");
        let mut conn = self.get_connection().await?;
        self.execute_statement_on_connection(statement, &mut conn)
            .await
    }

    #[instrument(skip(self, statement, conn), level = "debug")]
    async fn execute_statement_on_connection(
        &self,
        statement: &Statement,
        conn: &mut PooledConnection<'_>,
    ) -> Result<(), StorageError> {
        trace!("Executing statement on connection");
        conn.execute(statement.sql(), &statement.params())
            .await
            .map_err(|e| {
                error!(error = %e, "Failed to execute statement");
                StorageError::Other(format!("Failed to execute statement: {e}"))
            })?;
        debug!("Statement executed successfully");
        Ok(())
    }
}

impl MsSql {
    #[instrument(skip(self, config), level = "debug")]
    pub async fn get_vrf_key(
        &self,
        config: &VrfKeyConfig,
    ) -> Result<VrfKeyTableData, VrfKeyRetrievalError> {
        debug!("Retrieving VRF key from database");

        let mut conn = self.get_connection().await.map_err(|err| {
            error!(%err, "Failed to get DB connection for VRF key retrieval");
            VrfKeyRetrievalError::DatabaseError
        })?;

        let statement = vrf_key::get_statement(&config).map_err(|err| {
            error!(%err, "Failed to build VRF key retrieval statement");
            VrfKeyRetrievalError::CorruptedData
        })?;
        let query_stream = conn
            .query(statement.sql(), &statement.params())
            .await
            .map_err(|err| {
                error!(%err, "Failed to execute VRF key retrieval query");
                VrfKeyRetrievalError::DatabaseError
            })?;

        let row = query_stream.into_row().await.map_err(|err| {
            error!(%err, "Failed to fetch VRF key row");
            VrfKeyRetrievalError::DatabaseError
        })?;

        match row {
            None => {
                debug!("VRF key not found");
                return Err(VrfKeyRetrievalError::KeyNotFound);
            }
            Some(row) => {
                debug!("VRF key found");
                vrf_key::from_row(&row).map_err(|err| {
                    error!(%err, "Failed to parse VRF key from database row");
                    VrfKeyRetrievalError::CorruptedData
                })
            }
        }
    }

    #[instrument(skip(self, table_data), level = "debug")]
    pub async fn store_vrf_key(
        &self,
        table_data: &VrfKeyTableData,
    ) -> Result<(), VrfKeyStorageError> {
        debug!("Storing VRF key in database");

        let statement = vrf_key::store_statement(table_data);
        self.execute_statement(&statement).await.map_err(|err| {
            error!(%err, "Failed to store VRF key in database");
            VrfKeyStorageError
        })
    }
}

#[async_trait]
impl Database for MsSql {
    #[instrument(skip(self, record), level = "debug")]
    async fn set(&self, record: DbRecord) -> Result<(), StorageError> {
        debug!(
            record_type = {
                match &record {
                    DbRecord::Azks(_) => "Azks",
                    DbRecord::TreeNode(_) => "TreeNode",
                    DbRecord::ValueState(_) => "ValueState",
                }
            },
            "Setting single record"
        );
        let statement = record.set_statement()?;
        self.execute_statement(&statement).await
    }

    #[instrument(skip(self, records, _state), fields(record_count = records.len()), level = "info")]
    async fn batch_set(
        &self,
        records: Vec<DbRecord>,
        _state: DbSetState, // TODO: unused in mysql example, but may be needed later
    ) -> Result<(), StorageError> {
        let total_records = records.len();
        info!(total_records, "Starting batch_set operation");

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

        debug!(group_count = groups.len(), "Records grouped by type");
        for (storage_type, group) in &groups {
            debug!(storage_type = ?storage_type, record_count = group.len(), "Group details");
        }

        // Execute each group in batches
        let mut conn = self.get_connection().await?;
        // Start transaction
        info!("Beginning transaction for batch_set");
        conn.simple_query("BEGIN TRANSACTION").await.map_err(|e| {
            error!(error = %e, "Failed to begin transaction");
            StorageError::Transaction(format!("Failed to begin transaction: {e}"))
        })?;
        let result = async {
            for (storage_type, mut record_group) in groups.into_iter() {
                if record_group.is_empty() {
                    debug!(storage_type = ?storage_type, "Skipping empty group");
                    continue;
                }

                info!(storage_type = ?storage_type, record_count = record_group.len(), "Processing batch");

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
                debug!(table_name = %table, "Creating temp table");
                conn.simple_query(&table.create()).await.map_err(|e| {
                    error!(error = ?e, ?storage_type, "Failed to create temp table");
                    StorageError::Other(format!("Failed to create temp table: {e}"))
                })?;

                // Create bulk insert
                let table_name = &table.to_string();
                debug!(table_name, ?storage_type, "Starting bulk insert");
                let mut bulk = conn.bulk_insert(table_name).await.map_err(|e| {
                    error!(error = ?e, ?storage_type, "Failed to start bulk insert");
                    StorageError::Other(format!("Failed to start bulk insert: {e}"))
                })?;

                for record in &record_group {
                    let row = record.into_row()?;
                    bulk.send(row).await.map_err(|e| {
                        error!(error = ?e, ?storage_type, "Failed to add row to bulk insert");
                        StorageError::Other(format!("Failed to add row to bulk insert: {e}"))
                    })?;
                }

                debug!(row_count = record_group.len(), "Finalizing bulk insert");
                bulk.finalize().await.map_err(|e| {
                    error!(error = ?e, "Failed to finalize bulk insert");
                    StorageError::Other(format!("Failed to finalize bulk insert: {e}"))
                })?;

                // Set values from temp table to main table
                debug!("Merging temp table data into main table");
                let sql = <DbRecord as AkdStorableForMsSql>::set_batch_statement(&storage_type);
                trace!(sql, "Batch merge SQL");
                conn.simple_query(&sql).await.map_err(|e| {
                    error!(error = %e, "Failed to execute batch set statement");
                    StorageError::Other(format!("Failed to execute batch set statement: {e}"))
                })?;

                // Delete the temp table
                debug!("Dropping temp table");
                conn.simple_query(&table.drop()).await.map_err(|e| {
                    error!(error = %e, "Failed to drop temp table");
                    StorageError::Other(format!("Failed to drop temp table: {e}"))
                })?;
                info!(storage_type = ?storage_type, "Successfully processed batch");
            }

            Ok::<(), StorageError>(())
        };

        match result.await {
            Ok(_) => {
                info!("Committing transaction");
                conn.simple_query("COMMIT").await.map_err(|e| {
                    error!(error = %e, "Failed to commit transaction");
                    StorageError::Transaction(format!("Failed to commit transaction: {e}"))
                })?;
                info!(total_records, "batch_set completed successfully");
                Ok(())
            }
            Err(e) => {
                warn!(error = %e, "batch_set failed, rolling back transaction");
                conn.simple_query("ROLLBACK").await.map_err(|e| {
                    error!(error = %e, "Failed to roll back transaction");
                    StorageError::Transaction(format!("Failed to roll back transaction: {e}"))
                })?;
                error!(error = %e, "batch_set rolled back");
                Err(StorageError::Other(format!(
                    "Failed to batch set records: {e}"
                )))
            }
        }
    }

    #[instrument(skip(self, id), fields(storage_type = ?St::data_type()), level = "debug")]
    async fn get<St: Storable>(&self, id: &St::StorageKey) -> Result<DbRecord, StorageError> {
        let data_type = St::data_type();
        debug!(?data_type, "Getting single record");
        let mut conn = self.get_connection().await?;
        let statement = DbRecord::get_statement::<St>(id)?;
        trace!(sql = statement.sql(), "Get SQL");

        let query_stream = conn
            .query(statement.sql(), &statement.params())
            .await
            .map_err(|e| {
                error!(error = %e, ?id, ?data_type, sql = statement.sql(), "Failed to execute query");
                StorageError::Other(format!("Failed to execute query: {e}"))
            })?;

        let row = query_stream.into_row().await.map_err(|e| {
            error!(error = %e, ?id, ?data_type, "Failed to fetch row");
            StorageError::Other(format!("Failed to fetch row: {e}"))
        })?;

        if let Some(row) = row {
            debug!(?id, ?data_type, "Record found");
            DbRecord::from_row::<St>(&row)
        } else {
            debug!(?id, ?data_type, "Record not found");
            Err(StorageError::NotFound(format!(
                "{:?} {:?} not found",
                St::data_type(),
                id
            )))
        }
    }

    #[instrument(skip(self, ids), fields(storage_type = ?St::data_type(), id_count = ids.len()), level = "debug")]
    async fn batch_get<St: Storable>(
        &self,
        ids: &[St::StorageKey],
    ) -> Result<Vec<DbRecord>, StorageError> {
        if ids.is_empty() {
            debug!("batch_get called with empty ids, returning empty vector");
            return Ok(vec![]);
        }

        debug!(id_count = ids.len(), "Starting batch_get");

        let temp_table = TempTable::for_ids::<St>();
        if !temp_table.can_create() {
            warn!(storage_type = ?St::data_type(), "Cannot create temp table, batch get not supported");
            return Ok(Vec::new());
        }
        let create_temp_table = temp_table.create();
        let temp_table_name = &temp_table.to_string();

        let mut conn = self.get_connection().await?;

        // Begin a transaction
        debug!("Beginning transaction for batch_get");
        conn.simple_query("BEGIN TRANSACTION").await.map_err(|e| {
            error!(error = %e, "Failed to begin transaction");
            StorageError::Transaction(format!("Failed to begin transaction: {e}"))
        })?;

        let result = async {
            // Use bulk_insert to insert all the ids into a temporary table
            debug!("Creating temp table for batch_get");
            conn.simple_query(&create_temp_table).await.map_err(|e| {
                error!(error = %e, "Failed to create temp table");
                StorageError::Other(format!("Failed to create temp table: {e}"))
            })?;
            let mut bulk = conn
                .bulk_insert(temp_table_name)
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
            debug!("Querying batch records from temp table");
            let get_sql = DbRecord::get_batch_statement::<St>();
            trace!(sql = get_sql, "Batch get SQL");
            let query_stream = conn.simple_query(&get_sql).await.map_err(|e| {
                error!(error = %e, "Failed to execute batch get query");
                StorageError::Other(format!("Failed to execute batch get query: {e}"))
            })?;
            let mut records = Vec::new();
            {
                let rows = query_stream.into_first_result().await.map_err(|e| {
                    error!(error = %e, "Failed to fetch rows");
                    StorageError::Other(format!("Failed to fetch rows: {e}"))
                })?;
                for row in rows {
                    let record = DbRecord::from_row::<St>(&row)?;
                    records.push(record);
                }
            }
            debug!(
                record_count = records.len(),
                "Retrieved records from batch_get"
            );

            debug!("Dropping temp table");
            let drop_temp_table = temp_table.drop();
            conn.simple_query(&drop_temp_table).await.map_err(|e| {
                error!(error = %e, "Failed to drop temp table");
                StorageError::Other(format!("Failed to drop temp table: {e}"))
            })?;

            Ok::<Vec<DbRecord>, StorageError>(records)
        };

        // Commit or rollback the transaction
        match result.await {
            Ok(records) => {
                debug!("Committing batch_get transaction");
                conn.simple_query("COMMIT").await.map_err(|e| {
                    error!(error = %e, "Failed to commit transaction");
                    StorageError::Transaction(format!("Failed to commit transaction: {e}"))
                })?;
                info!(
                    record_count = records.len(),
                    "batch_get completed successfully"
                );
                Ok(records)
            }
            Err(e) => {
                warn!(error = %e, "batch_get failed, rolling back");
                conn.simple_query("ROLLBACK").await.map_err(|e| {
                    error!(error = %e, "Failed to rollback transaction");
                    StorageError::Transaction(format!("Failed to rollback transaction: {e}"))
                })?;
                Err(e)
            }
        }
    }

    // Note: user and username here is the raw_label. The assumption is this is a single key for a single user, but that's
    // too restrictive for what we want, so generalize the name a bit.
    #[instrument(skip(self, raw_label), level = "debug")]
    async fn get_user_data(&self, raw_label: &AkdLabel) -> Result<types::KeyData, StorageError> {
        // Note: don't log raw_label or data as it may contain sensitive information, such as PII.
        debug!("Getting all data for label (raw_label not logged for privacy)");

        let result = async {
            let mut conn = self.get_connection().await?;

            let statement = values::get_all(raw_label);

            trace!(sql = statement.sql(), "Query SQL");
            let query_stream = conn
                .query(statement.sql(), &statement.params())
                .await
                .map_err(|e| {
                    error!(error = %e, "Failed to execute query for label");
                    StorageError::Other(format!("Failed to execute query for label: {e}"))
                })?;
            let mut states = Vec::new();
            {
                let rows = query_stream.into_first_result().await.map_err(|e| {
                    error!(error = %e, "Failed to fetch rows for label");
                    StorageError::Other(format!("Failed to fetch rows for label: {e}"))
                })?;
                for row in rows {
                    let record = statement.parse(&row)?;
                    states.push(record);
                }
            }
            debug!(state_count = states.len(), "Retrieved states for label");
            let key_data = KeyData { states };

            Ok::<KeyData, StorageError>(key_data)
        };

        match result.await {
            Ok(data) => {
                info!("get_user_data completed successfully");
                Ok(data)
            }
            Err(e) => {
                error!(error = %e, "Failed to get all data for label");
                Err(StorageError::Other(format!(
                    "Failed to get all data for label: {e}"
                )))
            }
        }
    }

    // Note: user and username here is the raw_label. The assumption is this is a single key for a single user, but that's
    // too restrictive for what we want, so generalize the name a bit.
    #[instrument(skip(self, raw_label), fields(flag = ?flag), level = "debug")]
    async fn get_user_state(
        &self,
        raw_label: &AkdLabel,
        flag: types::ValueStateRetrievalFlag,
    ) -> Result<types::ValueState, StorageError> {
        debug!(?flag, "Getting raw label with flag");
        let statement = values::get_by_flag(raw_label, flag);
        let mut conn = self.get_connection().await?;
        trace!(sql = statement.sql(), "Query SQL");
        let query_stream = conn
            .query(statement.sql(), &statement.params())
            .await
            .map_err(|e| {
                error!(error = ?e, "Failed to execute query");
                StorageError::Other(format!("Failed to execute query: {e}"))
            })?;
        let row = query_stream.into_row().await.map_err(|e| {
            error!(error = %e, "Failed to fetch first result row");
            StorageError::Other(format!("Failed to fetch first result row: {e}"))
        })?;
        if let Some(row) = row {
            debug!("Raw label found");
            statement.parse(&row)
        } else {
            debug!("Raw label not found");
            Err(StorageError::NotFound(
                "ValueState for label not found".to_string(),
            ))
        }
    }

    // Note: user and username here is the raw_label. The assumption is this is a single key for a single user, but that's
    // too restrictive for what we want, so generalize the name a bit.
    #[instrument(skip(self, raw_labels), fields(label_count = raw_labels.len(), flag = ?flag), level = "debug")]
    async fn get_user_state_versions(
        &self,
        raw_labels: &[AkdLabel],
        flag: types::ValueStateRetrievalFlag,
    ) -> Result<HashMap<AkdLabel, (u64, AkdValue)>, StorageError> {
        info!(label_count = raw_labels.len(), flag = ?flag, "Getting user state versions");
        let mut conn = self.get_connection().await?;

        let temp_table = TempTable::RawLabelSearch;
        let create_temp_table = temp_table.create();
        let temp_table_name = &temp_table.to_string();

        // Begin a transaction
        debug!("Beginning transaction for get_user_state_versions");
        conn.simple_query("BEGIN TRANSACTION").await.map_err(|e| {
            error!(error = %e, "Failed to begin transaction");
            StorageError::Transaction(format!("Failed to begin transaction: {e}"))
        })?;

        let result = async {
            conn.simple_query(&create_temp_table).await.map_err(|e| {
                error!(error = %e, "Failed to create temp table");
                StorageError::Other(format!("Failed to create temp table: {e}"))
            })?;

            // Use bulk_insert to insert all the raw_labels into a temporary table
            let mut bulk = conn
                .bulk_insert(temp_table_name)
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
            let statement = values::get_versions_by_flag(temp_table_name, flag);
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
