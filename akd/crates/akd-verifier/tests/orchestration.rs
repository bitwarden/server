//! Orchestration tests for the AKD verifier SDK.
//!
//! Each test exercises one specific behavior the SDK owns — value extraction,
//! per-item failure encoding, batch poisoning resistance, audit fan-out,
//! length guards, history mapping. Real `akd::Directory`-produced proofs go
//! through the wire via wiremock.

mod common;

use akd::{AkdLabel, AkdValue, Digest, HistoryProof, LookupProof};
use akd_verifier::error::{
    BatchLookupError, BatchVerifyError, LookupError, VerifyError, VerifyItemError,
};
use akd_verifier::models::{BitwardenAkdLabelMaterialRequest, BitwardenAkdPairMaterialRequest};
use akd_verifier::verifier::AkdVerifier;
use bitwarden_encoding::B64;
use uuid::Uuid;
use wiremock::matchers::{method, path, path_regex};
use wiremock::{Mock, MockServer, ResponseTemplate};

const NS: &str = "test-ns";

// ---------------- Label / pair helpers --------------------

fn label_request(user_id: Uuid) -> BitwardenAkdLabelMaterialRequest {
    BitwardenAkdLabelMaterialRequest::UserPublicKey { user_id }
}

fn pair_request(user_id: Uuid, value: &[u8]) -> BitwardenAkdPairMaterialRequest {
    BitwardenAkdPairMaterialRequest::UserPublicKey {
        user_id,
        public_key_der_b64: B64::from(value),
    }
}

fn akd_label(user_id: Uuid) -> AkdLabel {
    label_request(user_id).try_into().expect("label conversion")
}

// ---------------- Directory publish helpers --------------------

/// Publish a single (user, value) update; returns the new epoch.
async fn publish_user(dir: &common::TestDirectory, user_id: Uuid, value: &[u8]) -> u64 {
    dir.publish(vec![(akd_label(user_id), AkdValue(value.to_vec()))])
        .await
}

/// Publish multiple (user, value) updates as one epoch; returns the new epoch.
async fn publish_user_batch(dir: &common::TestDirectory, pairs: &[(Uuid, &[u8])]) -> u64 {
    let updates: Vec<_> = pairs
        .iter()
        .map(|&(user, val)| (akd_label(user), AkdValue(val.to_vec())))
        .collect();
    dir.publish(updates).await
}

// ---------------- Wiremock mounting helpers --------------------

async fn mount_public_key(reader: &MockServer, vrf_pk: &[u8]) {
    Mock::given(method("GET"))
        .and(path("/public_key"))
        .respond_with(ResponseTemplate::new(200).set_body_json(common::public_key_response(vrf_pk)))
        .mount(reader)
        .await;
}

/// Mount the reader's `/batch_lookup` endpoint to return the given proofs at
/// `(epoch, root)`. Order of `proofs` matches the order of labels in the
/// request — the SDK pairs them by index.
async fn mount_batch_lookup(reader: &MockServer, proofs: &[LookupProof], epoch: u64, root: Digest) {
    Mock::given(method("POST"))
        .and(path("/batch_lookup"))
        .respond_with(
            ResponseTemplate::new(200)
                .set_body_json(common::batch_lookup_response(proofs, epoch, root)),
        )
        .mount(reader)
        .await;
}

/// Mount the reader's `/key_history` endpoint to return the given history
/// proof at `(epoch, root)`.
async fn mount_key_history(reader: &MockServer, proof: &HistoryProof, epoch: u64, root: Digest) {
    Mock::given(method("POST"))
        .and(path("/key_history"))
        .respond_with(
            ResponseTemplate::new(200)
                .set_body_json(common::key_history_response(proof, epoch, root)),
        )
        .mount(reader)
        .await;
}

async fn mount_audited(watch: &MockServer, epoch: u64) {
    Mock::given(method("GET"))
        .and(path_regex(r"^/namespaces/.+/audits/\d+$"))
        .respond_with(ResponseTemplate::new(200).set_body_json(common::audited_response(epoch)))
        .mount(watch)
        .await;
}

async fn mount_unaudited(watch: &MockServer) {
    Mock::given(method("GET"))
        .and(path_regex(r"^/namespaces/.+/audits/\d+$"))
        .respond_with(ResponseTemplate::new(200).set_body_json(common::unaudited_response()))
        .mount(watch)
        .await;
}

async fn mount_audit_5xx(watch: &MockServer) {
    Mock::given(method("GET"))
        .and(path_regex(r"^/namespaces/.+/audits/\d+$"))
        .respond_with(ResponseTemplate::new(503).set_body_string("dead"))
        .mount(watch)
        .await;
}

fn verifier(reader: &MockServer, watch: &MockServer) -> AkdVerifier {
    AkdVerifier::new(
        reader.uri(),
        watch.uri(),
        NS.to_string(),
        common::TEST_INSTALLATION_ID,
    )
    .expect("construct verifier")
}

// ---------------- Tests --------------------

#[tokio::test]
async fn lookup_returns_published_value_bytes() {
    let dir = common::TestDirectory::new().await;
    let user = Uuid::nil();
    let value: &[u8] = b"trusted-public-key";
    publish_user(&dir, user, value).await;
    let (proof, root, epoch) = dir.lookup_proof(akd_label(user)).await;

    let reader = MockServer::start().await;
    let watch = MockServer::start().await;
    mount_public_key(&reader, &dir.vrf_public_key().await).await;
    mount_batch_lookup(&reader, &[proof], epoch, root).await;
    mount_audited(&watch, epoch).await;

    let v = verifier(&reader, &watch);
    let result = v.lookup(label_request(user)).await.expect("lookup ok");
    assert_eq!(result.value, value);
    assert_eq!(result.epoch, epoch);
}

#[tokio::test]
async fn verify_pair_matching_value_returns_verified() {
    let dir = common::TestDirectory::new().await;
    let user = Uuid::from_u128(0xa);
    let value: &[u8] = b"matching";
    publish_user(&dir, user, value).await;
    let (proof, root, epoch) = dir.lookup_proof(akd_label(user)).await;

    let reader = MockServer::start().await;
    let watch = MockServer::start().await;
    mount_public_key(&reader, &dir.vrf_public_key().await).await;
    mount_batch_lookup(&reader, &[proof], epoch, root).await;
    mount_audited(&watch, epoch).await;

    let v = verifier(&reader, &watch);
    let result = v.verify_pair(pair_request(user, value)).await.expect("ok");
    assert_eq!(result.value, value);
}

#[tokio::test]
async fn verify_pair_value_mismatch_surfaces_server_value() {
    let dir = common::TestDirectory::new().await;
    let user = Uuid::from_u128(0xb);
    let actual: &[u8] = b"actual-server-value";
    publish_user(&dir, user, actual).await;
    let (proof, root, epoch) = dir.lookup_proof(akd_label(user)).await;

    let reader = MockServer::start().await;
    let watch = MockServer::start().await;
    mount_public_key(&reader, &dir.vrf_public_key().await).await;
    mount_batch_lookup(&reader, &[proof], epoch, root).await;
    mount_audited(&watch, epoch).await;

    let v = verifier(&reader, &watch);
    let err = v
        .verify_pair(pair_request(user, b"stale-local-copy"))
        .await
        .expect_err("expected mismatch");
    let VerifyError::ValueMismatch {
        server_epoch,
        server_value,
        ..
    } = err
    else {
        panic!("wrong variant: {err:?}");
    };
    assert_eq!(server_value, actual);
    assert_eq!(server_epoch, epoch);
}

#[tokio::test]
async fn lookup_batch_one_tampered_proof_does_not_poison_batch() {
    let dir = common::TestDirectory::new().await;
    let user_a = Uuid::from_u128(1);
    let user_b = Uuid::from_u128(2);
    publish_user_batch(&dir, &[(user_a, b"value-a"), (user_b, b"value-b")]).await;
    let (proof_a, _, _) = dir.lookup_proof(akd_label(user_a)).await;
    let (proof_b, root, epoch) = dir.lookup_proof(akd_label(user_b)).await;

    let reader = MockServer::start().await;
    let watch = MockServer::start().await;
    mount_public_key(&reader, &dir.vrf_public_key().await).await;
    // Order matters: SDK matches input labels to returned proofs by index.
    let proofs = vec![proof_a, common::corrupt_proof(proof_b)];
    mount_batch_lookup(&reader, &proofs, epoch, root).await;
    mount_audited(&watch, epoch).await;

    let v = verifier(&reader, &watch);
    let err = v
        .lookup_batch(vec![label_request(user_a), label_request(user_b)])
        .await
        .expect_err("expected partial failure");
    let BatchLookupError::PerItem { verified, failed } = err else {
        panic!("expected PerItem, got {err:?}");
    };
    assert_eq!(verified.len(), 1);
    assert_eq!(verified[0].value.value, b"value-a");
    assert!(matches!(
        verified[0].input,
        BitwardenAkdLabelMaterialRequest::UserPublicKey { user_id } if user_id == user_a
    ));
    assert_eq!(failed.len(), 1);
    assert!(!failed[0].proof_error.is_empty());
    assert!(matches!(
        failed[0].input,
        BitwardenAkdLabelMaterialRequest::UserPublicKey { user_id } if user_id == user_b
    ));
}

#[tokio::test]
async fn verify_pairs_one_mismatch_does_not_poison_batch() {
    let dir = common::TestDirectory::new().await;
    let user_a = Uuid::from_u128(3);
    let user_b = Uuid::from_u128(4);
    let actual_a: &[u8] = b"a-real";
    let actual_b: &[u8] = b"b-real";
    publish_user_batch(&dir, &[(user_a, actual_a), (user_b, actual_b)]).await;
    let (proof_a, _, _) = dir.lookup_proof(akd_label(user_a)).await;
    let (proof_b, root, epoch) = dir.lookup_proof(akd_label(user_b)).await;

    let reader = MockServer::start().await;
    let watch = MockServer::start().await;
    mount_public_key(&reader, &dir.vrf_public_key().await).await;
    mount_batch_lookup(&reader, &[proof_a, proof_b], epoch, root).await;
    mount_audited(&watch, epoch).await;

    let v = verifier(&reader, &watch);
    let err = v
        .verify_pairs(vec![
            pair_request(user_a, actual_a),        // matches
            pair_request(user_b, b"stale-b-copy"), // mismatch
        ])
        .await
        .expect_err("expected partial failure");
    let BatchVerifyError::PerItem { verified, failed } = err else {
        panic!("expected PerItem, got {err:?}");
    };
    assert_eq!(verified.len(), 1);
    assert_eq!(verified[0].value.value, actual_a);
    assert_eq!(failed.len(), 1);
    let VerifyItemError::ValueMismatch {
        ref server_value, ..
    } = failed[0].error
    else {
        panic!("expected ValueMismatch, got {:?}", failed[0].error);
    };
    assert_eq!(server_value, actual_b);
    let BitwardenAkdPairMaterialRequest::UserPublicKey {
        user_id,
        ref public_key_der_b64,
    } = failed[0].input
    else {
        panic!("expected UserPublicKey, got {:?}", failed[0].input);
    };
    assert_eq!(user_id, user_b);
    assert_eq!(public_key_der_b64.as_bytes(), b"stale-b-copy");
}

#[tokio::test]
async fn audit_returns_null_yields_epoch_not_audited() {
    let dir = common::TestDirectory::new().await;
    let user = Uuid::from_u128(5);
    publish_user(&dir, user, b"v").await;
    let (proof, root, epoch) = dir.lookup_proof(akd_label(user)).await;

    let reader = MockServer::start().await;
    let watch = MockServer::start().await;
    mount_public_key(&reader, &dir.vrf_public_key().await).await;
    mount_batch_lookup(&reader, &[proof], epoch, root).await;
    mount_unaudited(&watch).await;

    let v = verifier(&reader, &watch);
    let err = v.lookup(label_request(user)).await.expect_err("Err");
    assert!(matches!(err, LookupError::EpochNotAudited { epoch: e } if e == epoch));
}

#[tokio::test]
async fn length_mismatch_yields_protocol_error() {
    let dir = common::TestDirectory::new().await;
    let user_a = Uuid::from_u128(6);
    let user_b = Uuid::from_u128(7);
    publish_user_batch(&dir, &[(user_a, b"a"), (user_b, b"b")]).await;
    let (proof_a, root, epoch) = dir.lookup_proof(akd_label(user_a)).await;

    let reader = MockServer::start().await;
    let watch = MockServer::start().await;
    mount_public_key(&reader, &dir.vrf_public_key().await).await;
    // Server returns 1 proof for 2 requested labels — protocol violation.
    mount_batch_lookup(&reader, &[proof_a], epoch, root).await;
    mount_audited(&watch, epoch).await;

    let v = verifier(&reader, &watch);
    let err = v
        .lookup_batch(vec![label_request(user_a), label_request(user_b)])
        .await
        .expect_err("Err");
    assert!(matches!(err, BatchLookupError::Protocol(_)));
}

#[tokio::test]
async fn lookup_history_returns_all_versions_in_order() {
    let dir = common::TestDirectory::new().await;
    let user = Uuid::from_u128(8);
    for value in [b"v1", b"v2", b"v3"] {
        publish_user(&dir, user, value).await;
    }
    let (proof, root, epoch) = dir.history_proof(&akd_label(user)).await;

    let reader = MockServer::start().await;
    let watch = MockServer::start().await;
    mount_public_key(&reader, &dir.vrf_public_key().await).await;
    mount_key_history(&reader, &proof, epoch, root).await;
    mount_audited(&watch, epoch).await;

    let v = verifier(&reader, &watch);
    let history = v.lookup_history(label_request(user)).await.expect("ok");
    let values: Vec<&[u8]> = history.iter().map(|h| h.value.as_slice()).collect();
    assert_eq!(values, vec![&b"v3"[..], &b"v2"[..], &b"v1"[..]]);
}

#[tokio::test]
async fn lookup_history_unaudited_yields_epoch_not_audited() {
    let dir = common::TestDirectory::new().await;
    let user = Uuid::from_u128(9);
    publish_user(&dir, user, b"v").await;
    let (proof, root, epoch) = dir.history_proof(&akd_label(user)).await;

    let reader = MockServer::start().await;
    let watch = MockServer::start().await;
    mount_public_key(&reader, &dir.vrf_public_key().await).await;
    mount_key_history(&reader, &proof, epoch, root).await;
    mount_unaudited(&watch).await;

    let v = verifier(&reader, &watch);
    let err = v
        .lookup_history(label_request(user))
        .await
        .expect_err("Err");
    assert!(matches!(err, LookupError::EpochNotAudited { epoch: e } if e == epoch));
}

#[tokio::test]
async fn lookup_batch_unaudited_yields_request_level_epoch_not_audited() {
    let dir = common::TestDirectory::new().await;
    let user_a = Uuid::from_u128(0x11);
    let user_b = Uuid::from_u128(0x12);
    publish_user_batch(&dir, &[(user_a, b"a"), (user_b, b"b")]).await;
    let (proof_a, _, _) = dir.lookup_proof(akd_label(user_a)).await;
    let (proof_b, root, epoch) = dir.lookup_proof(akd_label(user_b)).await;

    let reader = MockServer::start().await;
    let watch = MockServer::start().await;
    mount_public_key(&reader, &dir.vrf_public_key().await).await;
    mount_batch_lookup(&reader, &[proof_a, proof_b], epoch, root).await;
    mount_unaudited(&watch).await;

    let v = verifier(&reader, &watch);
    let err = v
        .lookup_batch(vec![label_request(user_a), label_request(user_b)])
        .await
        .expect_err("expected request-level failure");
    assert!(matches!(err, BatchLookupError::EpochNotAudited { epoch: e } if e == epoch));
}

#[tokio::test]
async fn verify_pairs_all_success_returns_clean_vec() {
    let dir = common::TestDirectory::new().await;
    let user_a = Uuid::from_u128(0x21);
    let user_b = Uuid::from_u128(0x22);
    let val_a: &[u8] = b"a-val";
    let val_b: &[u8] = b"b-val";
    publish_user_batch(&dir, &[(user_a, val_a), (user_b, val_b)]).await;
    let (proof_a, _, _) = dir.lookup_proof(akd_label(user_a)).await;
    let (proof_b, root, epoch) = dir.lookup_proof(akd_label(user_b)).await;

    let reader = MockServer::start().await;
    let watch = MockServer::start().await;
    mount_public_key(&reader, &dir.vrf_public_key().await).await;
    mount_batch_lookup(&reader, &[proof_a, proof_b], epoch, root).await;
    mount_audited(&watch, epoch).await;

    let v = verifier(&reader, &watch);
    let values = v
        .verify_pairs(vec![
            pair_request(user_a, val_a),
            pair_request(user_b, val_b),
        ])
        .await
        .expect("all pairs verify");
    assert_eq!(values.len(), 2);
    assert_eq!(values[0].value, val_a);
    assert_eq!(values[1].value, val_b);
}

#[tokio::test]
async fn audit_5xx_yields_connection_error() {
    let dir = common::TestDirectory::new().await;
    let user = Uuid::from_u128(10);
    publish_user(&dir, user, b"v").await;
    let (proof, root, epoch) = dir.lookup_proof(akd_label(user)).await;

    let reader = MockServer::start().await;
    let watch = MockServer::start().await;
    mount_public_key(&reader, &dir.vrf_public_key().await).await;
    mount_batch_lookup(&reader, &[proof], epoch, root).await;
    mount_audit_5xx(&watch).await;

    let v = verifier(&reader, &watch);
    let err = v.lookup(label_request(user)).await.expect_err("Err");
    assert!(matches!(err, LookupError::Connection(_)));
}
