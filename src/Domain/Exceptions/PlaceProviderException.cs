namespace Domain.Exceptions;

public class PlaceProviderException : Exception
{
    public string Provider { get; }

    public string Reason { get; }

    public PlaceProviderException(string provider, string reason)
    {
        Provider = provider;
        Reason = reason;
    }

    public PlaceProviderException(string provider, Exception inner)
    {
        Provider = provider;
        Reason = inner.Message;
    }
}