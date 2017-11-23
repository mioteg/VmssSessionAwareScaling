using Microsoft.Azure.Management.Compute.Fluent;

namespace AzureVmssSessionScaling.Core
{
    /// <summary>
    /// Information about a VM in  a VM Scale Set.
    /// The <see cref="InstanceId"/> is used by the <see cref="VmssCapacityManager"/> to interact with the <see cref="IVmssManager"/> to manage the VM Scale Set.
    /// The <see cref="IVmssLoadManager"/> can use various properties to determine the load and to distribute load. For example,
    /// if the <see cref="IVmssLoadManager"/> distributes sessions to clients, they may need connection information such as the <see cref="PrivateIpAddress"/>.
    /// Extend this class as needed and load the needed information in the <see cref="VmssManager"/>.
    /// </summary>
    public class VmssVmInfo
    {
        public string InstanceId { get; set; }
        public string Name { get; set; }
        public PowerState PowerState { get; set; }
        public string PrivateIpAddress { get; set; }
        public int? FaultDomain { get; set; }
        public int? UpdateDomain { get; set; }
    }
}
