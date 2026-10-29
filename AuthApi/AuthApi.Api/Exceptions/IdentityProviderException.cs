namespace AuthApi.Api.Exceptions;

public sealed class IdentityProviderException(string message, Exception? innerException = null)
    : Exception(message, innerException);
