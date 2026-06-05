```

BenchmarkDotNet v0.15.3, macOS 26.4.1 (25E253) [Darwin 25.4.0]
Apple M4 Max, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.107
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), Arm64 RyuJIT armv8.0-a


```
| Method                                                   | Mean      | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------------------------------------------- |----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| Reflection_Serialize_ResetPasswordDataModel              |  46.90 ns | 0.382 ns | 0.319 ns |  1.00 |    0.01 | 0.0095 |      80 B |        1.00 |
| SourceGen_Serialize_ResetPasswordDataModel               |  38.94 ns | 0.431 ns | 0.404 ns |  0.83 |    0.01 | 0.0095 |      80 B |        1.00 |
| Reflection_Serialize_Permissions                         | 155.95 ns | 1.316 ns | 1.099 ns |  3.33 |    0.03 | 0.0696 |     584 B |        7.30 |
| SourceGen_Serialize_Permissions                          | 138.89 ns | 2.701 ns | 3.111 ns |  2.96 |    0.07 | 0.0696 |     584 B |        7.30 |
| Reflection_Deserialize_ResetPasswordDataModel_CamelCase  |  70.92 ns | 0.982 ns | 0.871 ns |  1.51 |    0.02 | 0.0029 |      24 B |        0.30 |
| SourceGen_Deserialize_ResetPasswordDataModel_CamelCase   |  70.26 ns | 1.059 ns | 0.991 ns |  1.50 |    0.02 | 0.0029 |      24 B |        0.30 |
| Reflection_Deserialize_ResetPasswordDataModel_PascalCase |  69.24 ns | 0.808 ns | 0.755 ns |  1.48 |    0.02 | 0.0029 |      24 B |        0.30 |
| SourceGen_Deserialize_ResetPasswordDataModel_PascalCase  |  68.73 ns | 0.806 ns | 0.714 ns |  1.47 |    0.02 | 0.0029 |      24 B |        0.30 |
| Reflection_Deserialize_Permissions                       | 292.72 ns | 2.261 ns | 2.115 ns |  6.24 |    0.06 | 0.0038 |      32 B |        0.40 |
| SourceGen_Deserialize_Permissions                        | 517.13 ns | 3.360 ns | 2.806 ns | 11.03 |    0.09 | 0.0038 |      32 B |        0.40 |
