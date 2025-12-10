use akd::errors::StorageError;
use serde::{Deserialize, Serialize};

use crate::DatabaseType;

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type")]
pub enum DbConfig {
    MsSql {
        connection_string: String,
        pool_size: u32,
    },
}

impl DbConfig {
    pub async fn connect(&self) -> Result<DatabaseType, StorageError> {
        let db = match self {
            DbConfig::MsSql {
                connection_string,
                pool_size,
            } => {
                let db = crate::ms_sql::MsSql::builder(connection_string.clone())
                    .pool_size(pool_size.clone())
                    .build()
                    .await?;
                DatabaseType::MsSql(db)
            }
        };

        Ok(db)
    }
}
