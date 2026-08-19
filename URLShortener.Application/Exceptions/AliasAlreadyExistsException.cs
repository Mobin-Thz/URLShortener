namespace URLShortener.Application.Exceptions;

public sealed class AliasAlreadyExistsException(string alias)
    : Exception($"The alias '{alias}' is already in use.");
