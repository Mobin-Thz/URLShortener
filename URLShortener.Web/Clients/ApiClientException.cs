namespace URLShortener.Web.Clients;

public sealed class ApiClientException(
    int statusCode,
    string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
