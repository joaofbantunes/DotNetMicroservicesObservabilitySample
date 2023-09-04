namespace Shared;

public record StuffHappened(Guid Id, string What) : IEvent;