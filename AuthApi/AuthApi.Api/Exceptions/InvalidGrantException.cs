namespace AuthApi.Api.Exceptions;

public sealed class InvalidGrantException(string message) : Exception(message);
