use serde::Deserialize;

#[derive(Debug, Clone, Deserialize)]
#[serde(tag = "type")]
pub enum AuditStorageConfig {
    Filesystem { data_directory: String },
}
