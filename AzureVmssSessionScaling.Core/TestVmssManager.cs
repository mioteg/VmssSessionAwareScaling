using Microsoft.Azure.Management.Compute.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AzureVmssSessionScaling.Core
{
    /// <summary>
    /// Implementation if <see cref="IVmssManager"/> for testing purposes. Simulates add/deleting instances.
    /// </summary>
    public class TestVmssManager : IVmssManager
    {
        private List<VmssVmInfo> _vmList = new List<VmssVmInfo>();
        private int _lastInstanceId;
        private int _lastFaultDomain;
        private int _lastUpdateDomain;
        private const int _numberOfFaultDomains = 3;
        private const int _numberOfUpdateDomains = 5;
        
        public TestVmssManager()
        {
            _lastInstanceId = 0;
            AddInstances(Config.MinActiveInstances);
        }

        public Task AddInstancesAsync(int count, Action callback)
        {
            AddInstances(count);
            var task = new Task(callback);
            task.RunSynchronously();
            return task;
        }

        private void AddInstances(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var instance = new VmssVmInfo();
                instance.InstanceId = _lastInstanceId.ToString();
                instance.PowerState = PowerState.Running;
                instance.FaultDomain = _lastFaultDomain;
                instance.UpdateDomain = _lastUpdateDomain;
                _vmList.Add(instance);
                _lastInstanceId++;
                _lastFaultDomain = (_lastFaultDomain + 1) % _numberOfFaultDomains;
                _lastUpdateDomain = (_lastUpdateDomain + 1) % _numberOfUpdateDomains;
            }
        }

        public Task DeleteInstancesAsync(IEnumerable<string> instanceIds, Action callback)
        {
            _vmList.RemoveAll(vm => instanceIds.Contains(vm.InstanceId));
            var task = new Task(callback);
            task.RunSynchronously();
            return task;
        }

        public IEnumerable<VmssVmInfo> GetVirtualMachines()
        {
            return _vmList;
        }
    }
}
