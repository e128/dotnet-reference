using System.Collections.Generic;
using System.Text.Json.Serialization;
using E128.Reference.Core.Models;

namespace E128.Reference.Web;

/// <summary>
///     Source-generated JSON metadata for the minimal API endpoints. The generated
///     resolver replaces reflective metadata discovery on the request path.
/// </summary>
[JsonSerializable(typeof(Greeting))]
[JsonSerializable(typeof(GreetingRequest))]
[JsonSerializable(typeof(Greeting[]))]
[JsonSerializable(typeof(IReadOnlyList<Greeting>))]
internal sealed partial class AppJsonContext : JsonSerializerContext;
