namespace BLComponent.Exceptions;

public sealed class ScanQueueUnavailableException
    : Exception
{
    public ScanQueueUnavailableException() : base("Scan queue is full or closed.") {}
    
    public ScanQueueUnavailableException(string message) : base(message) {}
    
    public ScanQueueUnavailableException(string message, Exception innerException) : base(message, innerException) {}
}
