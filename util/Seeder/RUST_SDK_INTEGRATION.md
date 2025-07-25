# Rust SDK Integration Architecture

This document describes potential approaches for integrating the Rust SDK with the Database Seeder when it becomes available.

## Current Architecture

```
UserSeeder → ISeederCryptoService → SeederCryptoService (C# implementation)
```

## Potential Future Architecture

One possible approach could involve:

```
UserSeeder → ISeederCryptoService → RustSeederCryptoService → [Integration Layer] → Rust SDK
```

The integration layer could be implemented using various technologies such as:
- P/Invoke (Platform Invocation Services)
- gRPC or other RPC mechanisms
- WebAssembly integration
- Other FFI approaches

## Integration Readiness

The seeder has been designed with flexibility in mind through the `ISeederCryptoService` abstraction. This allows for different implementations without changing the core seeder logic.

### Current Preparation

1. **Abstraction Layer**
   - The `ISeederCryptoService` interface defines all crypto operations
   - This abstraction allows seamless switching between implementations

2. **Service Registration** (in ServiceCollectionExtension.cs)
   ```csharp
   // Current registration
   services.AddSingleton<ISeederCryptoService, SeederCryptoService>();
   
   // Example of possible future registration with feature flag
   if (configuration.GetValue<bool>("Seeder:UseRustSdk"))
   {
       // Future implementation could be registered here
       services.AddSingleton<ISeederCryptoService, RustSeederCryptoService>();
   }
   ```

3. **Configuration Support** (appsettings.json)
   ```json
   {
     "Seeder": {
       "UseRustSdk": false  // Could be toggled when ready
     }
   }
   ```

## Potential Integration Considerations

If the Rust SDK team chooses to expose functionality, some operations that might be useful include:

- Key derivation (PBKDF2/Argon2)
- Password hashing compatible with ASP.NET Core Identity
- Symmetric key generation
- Encryption operations (Bitwarden Type 2 format)
- RSA key pair generation
- Private key encryption
- Organization key generation
- General text encryption

## Example Integration Patterns

### P/Invoke Example (One Possible Approach)

If P/Invoke is chosen as the integration method, memory management patterns might look like:

```rust
// Example Rust side (if using P/Invoke)
#[no_mangle]
pub extern "C" fn generate_user_key(out_length: *mut usize) -> *mut u8 {
    // Implementation details would depend on Rust SDK design
}

#[no_mangle]
pub extern "C" fn free_buffer(ptr: *mut u8, length: usize) {
    // Memory cleanup
}
```

```csharp
// Example C# side (if using P/Invoke)
[DllImport("libsdk")]
private static extern IntPtr generate_user_key(out int length);

[DllImport("libsdk")]
private static extern void free_buffer(IntPtr ptr, int length);
```


## Benefits of Integration

Potential benefits of Rust SDK integration could include:

1. **Consistency**: Using the same crypto implementations as Bitwarden clients
2. **Performance**: Potential performance benefits from native code
3. **Maintainability**: Single source of truth for crypto operations
4. **Compatibility**: Ensuring seeded data matches production requirements

## Current State

The seeder infrastructure is prepared for integration:

- ✅ Abstraction layer exists (ISeederCryptoService)
- ✅ C# implementation works (SeederCryptoService)
- ✅ Rust integration stub created (RustSeederCryptoService)
- ✅ UserSeeder uses the abstraction
- ✅ OrganizationSeeder uses the abstraction
- ✅ All recipes accept ISeederCryptoService through dependency injection
- ✅ Service registration supports feature flags
- ✅ Configuration structure in place
- ⏳ Ready for Rust SDK team collaboration
- ⏳ Integration approach to be determined based on Rust SDK design

## Recipe Readiness

All recipes are prepared to work with any ISeederCryptoService implementation:
- ✅ OrganizationWithUsersRecipe
- ✅ OrganizationWithUsersAndVaultItemsRecipe  
- ✅ UserWithVaultItemsRecipe
- ✅ VaultItemsRecipe

## Next Steps

The seeder architecture is ready for integration. The specific integration approach will depend on:
- How the Rust SDK team chooses to expose functionality
- Performance requirements
- Cross-platform compatibility needs
- Maintenance and deployment considerations

