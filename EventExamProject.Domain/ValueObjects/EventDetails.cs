namespace EventExamProject.Domain.ValueObjects;

public record EventDetails(string Title, string? Description, DateTime StartAt, DateTime EndAt, int TotalSeats);
