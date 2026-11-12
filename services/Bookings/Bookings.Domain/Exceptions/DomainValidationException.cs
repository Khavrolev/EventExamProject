namespace Bookings.Domain.Exceptions;

public class DomainValidationException(string message) : Exception(message);
