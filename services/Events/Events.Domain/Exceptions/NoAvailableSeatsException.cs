namespace Events.Domain.Exceptions;

public class NoAvailableSeatsException(string message) : Exception(message);
