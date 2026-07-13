using EventExamProject.DTOs;
using EventExamProject.Models;
using EventExamProject.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EventExamProject.Controllers;

[ApiController]
[Route("events")]
public class EventsController(IEventService eventService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Event>>> GetAllEvents()
    {
        return Ok(await eventService.GetAllEvents());
    }
    
    [HttpGet("{id:Guid}")]
    public async Task<ActionResult<Event>> GetEventById(Guid id)
    {
        var eventById = await eventService.GetEventById(id);
        
        return Ok(eventById);
    }

    [HttpPost]
    public async Task<ActionResult<Event>> AddEvent(EventDto newEvent)
    {
        var created = await eventService.AddEvent(newEvent);

        return CreatedAtAction(nameof(GetEventById), new
        {
            id = created.Id
        }, created);
    }

    [HttpPut("{id:Guid}")]
    public async Task<ActionResult<Event>> UpdateEvent(Guid id, EventDto newEvent)
    {
        var updated = await eventService.UpdateEvent(id, newEvent);
        
        return Ok(updated);
    }

    [HttpDelete("{id:Guid}")]
    public async Task<ActionResult> DeleteEvent(Guid id)
    {
        var deleted = await eventService.DeleteEvent(id);
        
        return NoContent();
    }
}