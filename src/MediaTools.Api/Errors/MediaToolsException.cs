namespace MediaTools.Api.Errors;

public abstract class MediaToolsException : Exception
{
    protected MediaToolsException(
        string errorCode,
        int statusCode,
        string publicMessage,
        Exception? innerException = null)
        : base(publicMessage, innerException)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    public string ErrorCode { get; }

    public int StatusCode { get; }
}
