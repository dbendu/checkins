namespace CheckIn.Api.Controllers.CheckIns;

public sealed record VisitorResponse(
    long UserId,
    string DisplayName,
    bool HasPhoto,
    DateTimeOffset CreatedAt
);
