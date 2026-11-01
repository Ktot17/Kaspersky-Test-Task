namespace BLComponent.Exceptions;

public class ScanFailedException : Exception
{
    public ScanFailedException() : base("Scan failed.") {}
    
    public ScanFailedException(string message) : base(message) {}
    
    public ScanFailedException(string message, Exception innerException) : base(message, innerException) {}
}
