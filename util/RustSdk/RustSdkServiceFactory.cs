﻿namespace Bit.RustSDK;

/// <summary>
/// Factory for creating Rust SDK service instances
/// </summary>
public static class RustSdkServiceFactory
{
    /// <summary>
    /// Creates a singleton instance of the Rust SDK service (thread-safe)
    /// </summary>
    /// <returns>A singleton IRustSdkService instance</returns>
    public static RustSdkService CreateSingleton()
    {
        return SingletonHolder.Instance;
    }

    private static class SingletonHolder
    {
        internal static readonly RustSdkService Instance = new();
    }
}
