namespace EventExamProject.Domain.Exceptions;

public class NotFoundException(string message) : Exception(message);