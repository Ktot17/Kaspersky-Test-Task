namespace BLComponent.InputPorts;

public interface IRunningScanRegistry
{
    public void Register(
        Guid scanId,
        CancellationTokenSource cts);
    
    public bool TryCancel(
        Guid scanId);
    
    public void Unregister(
        Guid scanId);
}
