using System.Net;

namespace PaperAPI.Application.Billing.Exceptions;

public sealed class BillingException : Exception
{
    public BillingException(string error, string message, HttpStatusCode statusCode)
        : base(message)
    {
        Error = error;
        StatusCode = statusCode;
    }

    public string Error { get; }
    public HttpStatusCode StatusCode { get; }
}
