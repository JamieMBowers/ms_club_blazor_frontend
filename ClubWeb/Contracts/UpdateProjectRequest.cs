namespace ClubWeb.Contracts;

public sealed record UpdateProjectRequest
{
    public required string Name { get; init; }
}
