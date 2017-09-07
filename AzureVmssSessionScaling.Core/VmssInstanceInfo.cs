using Microsoft.Azure.Management.Compute.Fluent;

namespace AzureVmssSessionScaling.Core
{
    /// <summary>
    /// Information about the VMSS instance for use by the <see cref="VmssCapacityManager"/>.
    /// </summary>
    public class VmssInstanceInfo
    {
        public VmssInstanceInfo(VmssVmInfo vm)
        {
            VmInfo = vm;
            Load = 0;

            if (vm.PowerState == PowerState.Running) State = VmssInstanceState.Running;
            else if (vm.PowerState == PowerState.Starting) State = VmssInstanceState.Starting;
            else if (vm.PowerState == PowerState.Unknown) State = VmssInstanceState.Unknown;
            else if (vm.PowerState == PowerState.Deallocated || vm.PowerState == PowerState.Deallocating ||
                     vm.PowerState == PowerState.Stopped || vm.PowerState == PowerState.Stopping)
            {
                State = VmssInstanceState.Removed;
            }
        }


        /// <summary>
        /// Shorthand for <see cref="VmInfo.InstanceId"/>
        /// </summary>
        public string InstanceId
        {
            get { return this.VmInfo.InstanceId; }
        }

        /// <summary>
        /// Information about the VM.
        /// </summary>
        public VmssVmInfo VmInfo { get; private set; }

        /// <summary>
        /// State of the VM as relevant for the <see cref="VmssCapacityManager"/>.
        /// </summary>
        public VmssInstanceState State {get; set;}

        /// <summary>
        /// Load on the VM.
        /// </summary>
        public int Load { get; set; }
    }
}
