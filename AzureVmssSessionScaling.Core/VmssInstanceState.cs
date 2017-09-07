namespace AzureVmssSessionScaling.Core
{
    /// <summary>
    /// Indicates the state of a VM in a VM Scale Set.
    /// Similar to <see cref="Microsoft.Azure.Management.Compute.Fluent.PowerState"/>, but with additional properties to indicate whether the instance is accepting new load.
    /// </summary>
    public enum VmssInstanceState
    {
        Closing, // Marked to no longer accept connectiong, awaiting confirmation.
        Closed, // No longer accepting new connections.
        Opening, // Marked to start accepting sessions (again).
        Removed,
        Running,
        Starting,
        Unknown
    }
}
