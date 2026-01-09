using System.Net;

namespace PaperAPI.Application.Access.Exceptions;

public sealed class AccessException : Exception
{
    public AccessException(string error, string message, HttpStatusCode statusCode)
        : base(message)
    {
        Error = error;
        StatusCode = statusCode;
    }

    public string Error { get; }
    public HttpStatusCode StatusCode { get; }
}
