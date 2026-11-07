namespace ClubWeb.Contracts;

public sealed record CreateProjectRequest
{
    public required string Name { get; init; }
}
