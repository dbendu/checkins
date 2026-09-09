namespace Domain.Exceptions;

public sealed class CheckInTooSoonException(DateTimeOffset retryAfter)
    : Exception("В этом месте вы отмечались недавно.")
{
    public DateTimeOffset RetryAfter { get; } = retryAfter;
}
