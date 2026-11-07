using System.Text.Json.Serialization;

namespace ClubWeb.Contracts;

public sealed record ProjectResponse(
    [property: JsonRequired] int Id,
    [property: JsonRequired] string Name);
