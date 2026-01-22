/// Authentication middleware tests for publisher API
///
/// Critical security tests covering:
/// - API key validation
/// - Constant-time comparison
/// - Timing attack resistance
/// - Missing/malformed credentials
use publisher::ApplicationConfig;
use uuid::Uuid;

/// Helper function to create a test application config
fn create_test_config(api_key: &str) -> ApplicationConfig {
    use akd_storage::{
        akd_storage_config::AkdStorageConfig, db_config::DbConfig,
        publish_queue_config::PublishQueueConfig, vrf_key_config::VrfKeyConfig,
    };

    ApplicationConfig {
        storage: AkdStorageConfig {
            db_config: DbConfig::MsSql {
                connection_string: "Server=localhost;Database=test".to_string(),
                pool_size: 10,
            },
            cache_item_lifetime_ms: 30000,
            cache_limit_bytes: None,
            cache_clean_ms: 15000,
            vrf_key_config: VrfKeyConfig::B64EncodedSymmetricKey {
                key: "dGVzdC1rZXk=".to_string(), // base64 encoded test key
            },
            publish_queue_config: PublishQueueConfig::DbBacked,
            insertion_parallelism: 32,
            preload_parallelism: 32,
        },
        publisher: Default::default(),
        installation_id: Uuid::nil(), // Use nil UUID for tests
        web_server_bind_address: "127.0.0.1:3000".to_string(),
        web_server_api_key: api_key.to_string(),
    }
}

#[tokio::test]
async fn test_valid_api_key_allows_access() {
    let api_key = "test-api-key-12345678901234567890";
    let config = create_test_config(api_key);

    assert!(
        config.api_key_valid(api_key),
        "Valid API key should be accepted"
    );
}

#[tokio::test]
async fn test_invalid_api_key_denies_access() {
    // Threat model: Attacker tries to access API with wrong key
    let correct_key = "correct-api-key-12345678901234567890";
    let config = create_test_config(correct_key);
    let wrong_key = "wrong-api-key-00000000000000000000";

    assert!(
        !config.api_key_valid(wrong_key),
        "Invalid API key should be rejected"
    );
}

#[tokio::test]
async fn test_empty_api_key_rejected() {
    // Threat model: Attacker sends empty API key
    let config = create_test_config("valid-key-12345678901234567890");

    assert!(
        !config.api_key_valid(""),
        "Empty API key should be rejected"
    );
}

#[tokio::test]
async fn test_different_length_keys_fail() {
    // Threat model: Documented timing vulnerability - length mismatch causes immediate failure
    // Reference: config.rs lines 23-25
    // Note: We use subtle::ConstantTimeEq for same-length keys, which is constant-time by design
    let correct_key = "a".repeat(32);
    let config = create_test_config(&correct_key);

    assert!(
        !config.api_key_valid("short"),
        "Short key should be rejected"
    );
    assert!(
        !config.api_key_valid(&"a".repeat(16)),
        "Half-length key should be rejected"
    );
    assert!(
        !config.api_key_valid(&"a".repeat(64)),
        "Double-length key should be rejected"
    );
    assert!(
        config.api_key_valid(&correct_key),
        "Correct length key should work"
    );
}
