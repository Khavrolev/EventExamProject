using EventExamProject.DataAccess.Interfaces;
using EventExamProject.Exceptions;
using EventExamProject.Models;

namespace EventExamProject.DataAccess;

public class InMemoryBookingStore : IBookingStore
{
    private readonly List<Booking> _bookings = [];

    public void Add(Booking booking)
    {
        _bookings.Add(booking);
    }
    
    public Booking? GetById(Guid id)
    {
        return _bookings.Find(b => b.Id == id);
    }
    
    public IEnumerable<Booking> GetByStatus(BookingStatus status)
    {
        return _bookings.Where(b => b.Status == status);
    }
    
    public void Update(Booking booking)
    {
        var index = _bookings.FindIndex(b => b.Id == booking.Id);

        if (index == -1)
        {
            throw new NotFoundException($"Event with id {booking.Id} was not found");
        }
        
        _bookings[index] = booking;
    }
}