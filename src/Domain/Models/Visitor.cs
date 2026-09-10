namespace Domain.Models;

public sealed record Visitor(
    long Id,
    string DisplayName,
    bool HasPhoto
);
