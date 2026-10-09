namespace EventExamProject.Domain.Exceptions;

public class NoAvailableSeatsException(string message) : Exception(message);