using EventExamProject.Models;

namespace EventExamProject.DataAccess.Interfaces;

public interface IBookingStore
{
    void Add(Booking booking);
    Booking? GetById(Guid id);
    IEnumerable<Booking> GetByStatus(BookingStatus status);
    void Update(Booking booking);
}