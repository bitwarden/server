use bitwarden_encoding::B64;
use uniffi;
#[cfg(feature = "uniffi")]
use uuid::Uuid;

// uniffi can't derive FfiConverter for foreign types like `Uuid` and `B64`.
// `custom_type!` registers a String-based representation once, after which
// any `#[derive(uniffi::Record)]` containing these fields lifts/lowers them
// transparently across the FFI. The `remote` keyword switches the impl from a
// blanket `impl<UT>` (orphan-rule-blocked for foreign types) to a concrete
// `impl FfiConverter<crate::UniFfiTag>`, valid here.
#[cfg(feature = "uniffi")]
uniffi::custom_type!(Uuid, String, {
    remote,
    lower: |uuid| uuid.to_string(),
    try_lift: |s| Ok(s.parse()?),
});

#[cfg(feature = "uniffi")]
uniffi::custom_type!(B64, String, {
    remote,
    lower: |b64| String::from(&b64),
    try_lift: |s| Ok(s.parse()?),
});
