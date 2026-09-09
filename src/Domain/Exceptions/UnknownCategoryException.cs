namespace Domain.Exceptions;

public sealed class UnknownCategoryException(string category) : Exception
{
    public string Category { get; } = category;
}