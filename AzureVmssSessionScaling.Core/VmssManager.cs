using Microsoft.Azure.Management.Compute.Fluent;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AzureVmssSessionScaling.Core
{
    public class VmssManager : IVmssManager
    {

        private IAzure _azure;
        private IVirtualMachineScaleSet _vmss;

        public VmssManager()
        {
            var credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(Config.ApplicationId, Config.ApplicationKey, Config.DirectoryId, AzureEnvironment.AzureGlobalCloud);
            _azure = Microsoft.Azure.Management.Fluent.Azure.Configure().Authenticate(credentials).WithDefaultSubscription();
            _vmss = _azure.VirtualMachineScaleSets.GetByResourceGroup(Config.ResourceGroupName, Config.VmssName);
        }

        public VmssManager(string resourceGroupName, string vmssName, IAzure azure)
        {
            _azure = azure;
            _vmss = _azure.VirtualMachineScaleSets.GetByResourceGroup(resourceGroupName, vmssName);
        }

        public async Task AddInstancesAsync(int count, Action callback)
        {
            if (count > 0)
            {
                await _vmss.Update()
                           .WithCapacity(_vmss.Capacity + count)
                           .ApplyAsync();
            }
            callback();
        }

        public async Task DeleteInstancesAsync(IEnumerable<string> instanceIds, Action callback)
        {
            foreach(var id in instanceIds)
            {
                var vm = _vmss.VirtualMachines.List().SingleOrDefault(i => i.InstanceId == id);
                if (vm != null) await vm.DeleteAsync();
            }
            callback();
        }

        public IEnumerable<VmssVmInfo> GetVirtualMachines()
        {
            var vmList = new List<VmssVmInfo>();
            foreach(var vm in _vmss.VirtualMachines.List())
            {
                var vmInfo = new VmssVmInfo();

                vmInfo.InstanceId = vm.InstanceId;
                vmInfo.Name = vm.Name;
                vmInfo.PowerState = vm.PowerState;
                vmInfo.PrivateIpAddress = vm.ListNetworkInterfaces().First().PrimaryPrivateIP;
                vmInfo.FaultDomain = vm.InstanceView.PlatformFaultDomain;
                vmInfo.UpdateDomain = vm.InstanceView.PlatformUpdateDomain;
                vmList.Add(vmInfo);
            }
            return vmList;
        }
    }
}
