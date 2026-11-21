using System.Collections.Generic;
using System.Text.Json.Serialization;
using PuzzlerGui.Models;

namespace PuzzlerGui.Services;

/// <summary>
/// Source-generated JSON metadata required by Native AOT and trimming.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(SolveReport))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
