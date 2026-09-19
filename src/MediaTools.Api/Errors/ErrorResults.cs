namespace MediaTools.Api.Errors;

public static class ErrorResults
{
    public static IResult Create(string errorCode) =>
        ErrorResponseCatalog.Result(errorCode);
}
