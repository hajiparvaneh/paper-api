using System.Net;

namespace PaperAPI.Application.Identity.Exceptions;

public sealed class IdentityException : Exception
{
    public IdentityException(string error, string message, HttpStatusCode statusCode)
        : base(message)
    {
        Error = error;
        StatusCode = statusCode;
    }

    public string Error { get; }

    public HttpStatusCode StatusCode { get; }
}
