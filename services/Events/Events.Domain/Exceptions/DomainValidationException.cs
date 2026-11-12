namespace Events.Domain.Exceptions;

public class DomainValidationException(string message) : Exception(message);
