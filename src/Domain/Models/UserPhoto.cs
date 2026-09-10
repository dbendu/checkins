namespace Domain.Models;

public sealed record UserPhoto(
    string ContentType,
    byte[] Data
);
