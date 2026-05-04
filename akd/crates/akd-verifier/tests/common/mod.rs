//! Test fixture for orchestration tests.
//!
//! Wraps an in-memory `akd::Directory` so tests can produce real, verifiable
//! `LookupProof` and `HistoryProof` values, plus helpers to wrap them in the
//! reader/watch wire shapes that the SDK expects.
//!
//! All tests in this binary share one process, so the
//! `BitwardenV1Configuration` is initialized exactly once via a `OnceLock`
//! guard with `Uuid::nil()` as the installation id. Tests that construct an
//! `AkdVerifier` must use the same id.

use std::sync::OnceLock;

use akd::ecvrf::HardCodedAkdVRF;
use akd::storage::manager::StorageManager;
use akd::storage::memory::AsyncInMemoryDatabase;
use akd::{
    AkdLabel, AkdValue, AzksParallelismConfig, Digest, EpochHash, HistoryParams, HistoryProof,
    LookupProof,
};
use bitwarden_akd_configuration::BitwardenV1Configuration;
use uuid::Uuid;

type Inner = akd::Directory<BitwardenV1Configuration, AsyncInMemoryDatabase, HardCodedAkdVRF>;

static INIT: OnceLock<()> = OnceLock::new();
pub const TEST_INSTALLATION_ID: Uuid = Uuid::nil();

pub struct TestDirectory {
    directory: Inner,
}

impl TestDirectory {
    pub async fn new() -> Self {
        INIT.get_or_init(|| {
            BitwardenV1Configuration::init(TEST_INSTALLATION_ID);
        });
        let db = AsyncInMemoryDatabase::new();
        let storage = StorageManager::new_no_cache(db);
        let vrf = HardCodedAkdVRF {};
        let directory = akd::Directory::<BitwardenV1Configuration, _, _>::new(
            storage,
            vrf,
            AzksParallelismConfig::default(),
        )
        .await
        .expect("directory init");
        Self { directory }
    }

    /// Publish a batch of (label, value) updates as one epoch; returns the new epoch.
    pub async fn publish(&self, updates: Vec<(AkdLabel, AkdValue)>) -> u64 {
        let EpochHash(epoch, _) = self.directory.publish(updates).await.expect("publish");
        epoch
    }

    pub async fn lookup_proof(&self, label: AkdLabel) -> (LookupProof, Digest, u64) {
        let (proof, EpochHash(epoch, root)) = self.directory.lookup(label).await.expect("lookup");
        (proof, root, epoch)
    }

    pub async fn history_proof(&self, label: &AkdLabel) -> (HistoryProof, Digest, u64) {
        let (proof, EpochHash(epoch, root)) = self
            .directory
            .key_history(label, HistoryParams::Complete)
            .await
            .expect("key_history");
        (proof, root, epoch)
    }

    pub async fn vrf_public_key(&self) -> Vec<u8> {
        self.directory
            .get_public_key()
            .await
            .expect("vrf pk")
            .as_bytes()
            .to_vec()
    }
}

// ------------------------------------------------------------------
// Wire response builders matching `akd-verifier/src/models.rs`.
// ------------------------------------------------------------------

pub fn batch_lookup_response(
    proofs: &[LookupProof],
    epoch: u64,
    root: Digest,
) -> serde_json::Value {
    serde_json::json!({
        "success": true,
        "data": {
            "lookup_proofs": serde_json::to_value(proofs).expect("serialize proofs"),
            "current_epoch_data": epoch_data(epoch, root),
        }
    })
}

pub fn key_history_response(proof: &HistoryProof, epoch: u64, root: Digest) -> serde_json::Value {
    serde_json::json!({
        "success": true,
        "data": {
            "history_proof": serde_json::to_value(proof).expect("serialize history proof"),
            "epoch_data": epoch_data(epoch, root),
        }
    })
}

pub fn public_key_response(vrf_pk: &[u8]) -> serde_json::Value {
    serde_json::json!({
        "success": true,
        "data": bitwarden_encoding::B64::from(vrf_pk),
    })
}

/// `audit-watch` returns this when an epoch *is* audited. Field values other
/// than `epoch` are not validated by the SDK in v1 (existence-only).
pub fn audited_response(epoch: u64) -> serde_json::Value {
    serde_json::json!({
        "epoch": epoch,
        "digest": "test-digest",
        "signature": "test-signature",
        "key_id": "test-key",
        "timestamp": 0u64,
    })
}

/// `audit-watch` returns null body (200 OK) when an epoch is not yet audited.
pub fn unaudited_response() -> serde_json::Value {
    serde_json::Value::Null
}

fn epoch_data(epoch: u64, root: Digest) -> serde_json::Value {
    serde_json::json!({
        "epoch": epoch,
        "epoch_hash_b64": bitwarden_encoding::B64::from(&root[..]),
    })
}

/// Mutate a `LookupProof` so it deserializes fine but fails verification.
/// Flips one byte of `commitment_nonce`, which is checked during verification.
pub fn corrupt_proof(mut p: LookupProof) -> LookupProof {
    if p.commitment_nonce.is_empty() {
        p.commitment_nonce.push(0xff);
    } else {
        p.commitment_nonce[0] ^= 0xff;
    }
    p
}
