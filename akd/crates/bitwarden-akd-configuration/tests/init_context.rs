//! Tests for the `init()` code path in multi-directory mode.
//!
//! These live in a separate integration test binary because `init()` sets a
//! process-global `AtomicBool` that cannot be reset, which would interfere with
//! unit tests that assert `hash()` panics without context.
//!
//! All assertions are in a single test because `init()` mutates global state
//! (`AtomicBool`, `RwLock`) that cannot be reset between tests.

#![cfg(feature = "multi_directory")]

use akd::configuration::Configuration;
use bitwarden_akd_configuration::BitwardenV1Configuration;
use uuid::Uuid;

#[test]
fn init_sets_default_context_overridable_by_with_installation() {
    let default_id = Uuid::nil();
    let override_id = Uuid::max();

    // Compute a reference hash for the override id before init
    let override_ref = BitwardenV1Configuration::with_installation(override_id, || {
        BitwardenV1Configuration::hash(b"data")
    });

    // --- init() enables hash() without with_installation ---
    BitwardenV1Configuration::init(default_id);
    let default_hash = BitwardenV1Configuration::hash(b"data");
    assert_ne!(default_hash, [0u8; 32]);
    let default_via_with = BitwardenV1Configuration::with_installation(default_id, || {
        BitwardenV1Configuration::hash(b"data")
    });
    assert_eq!(default_hash, default_via_with);

    // --- with_installation() overrides the init default ---
    let override_hash = BitwardenV1Configuration::with_installation(override_id, || {
        BitwardenV1Configuration::hash(b"data")
    });
    assert_ne!(default_hash, override_hash);
    assert_eq!(override_ref, override_hash);

    // --- init() flag is cross-thread ---
    let reference = BitwardenV1Configuration::hash(b"cross-thread");
    let handle = std::thread::spawn(move || BitwardenV1Configuration::hash(b"cross-thread"));
    let from_thread = handle.join().unwrap();
    assert_eq!(reference, from_thread);
}
