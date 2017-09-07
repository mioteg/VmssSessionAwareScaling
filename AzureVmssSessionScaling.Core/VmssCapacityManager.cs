using Microsoft.Azure.Management.Compute.Fluent;
using System.Collections.Generic;
using System.Linq;

namespace AzureVmssSessionScaling.Core
{
    /// <summary>
    /// Manages the capacity of a VM Scale Set based on the load on different VMs in the Scale Set.
    /// </summary>
    public class VmssCapacityManager
    {
        private List<VmssInstanceInfo> _vmssInstances;
        private IVmssManager _vmssManager;
        private IVmssLoadManager _vmssLoadManager;
        private bool _updating;
        private object _updatingLock = new object();

        public VmssCapacityManager(IVmssManager vmssManager, IVmssLoadManager loadManager)
        {
            _vmssManager = vmssManager;
            _vmssLoadManager = loadManager;
            _vmssInstances = new List<VmssInstanceInfo>();

            // Load store with information about virtual machine instances
            var virtualMachines = _vmssManager.GetVirtualMachines();
            foreach (var vm in virtualMachines)
            {
                AddInstance(vm);
            }
        }

        /// <summary>
        /// Updates the state of the VMSS and scales if necessary.
        /// Should be called periodically or after each change to the load.
        /// </summary>
        public void UpdateVmss()
        {
            UpdateVmssInfo();
            UpdateLoad();
            Scale();
        }

        /// <summary>
        /// Calculates the number of VM instances needed to satisfy the load requirements.
        /// </summary>
        /// <returns>Number of VM instances needed to satisfy the load requirements</returns>
        private int CalculateNeededCapacity()
        {
            int instancesNeeded = _vmssInstances.Count;
            int currentLoad = _vmssInstances.Sum(i => i.Load);
            int maxCapacity = Config.MaxLoadPerInstance * _vmssInstances.Count;
            int freeCapacity = maxCapacity - currentLoad;

            if (freeCapacity < Config.MinFreeVmssCapacity)
            {
                // Add new instances until free capacity requirement is met.
                int projectedFreeCapacity = freeCapacity;
                do
                {
                    instancesNeeded++;
                    projectedFreeCapacity += Config.MaxLoadPerInstance;
                } while (projectedFreeCapacity < Config.MinFreeVmssCapacity);
            }
            else
            {
                // Remove instances until free capacity requirement is met.
                int projectedFreeCapacity = freeCapacity;
                do
                {
                    if (instancesNeeded > Config.MinActiveInstances - 1)
                    {
                        instancesNeeded--;
                        projectedFreeCapacity -= Config.MaxLoadPerInstance;
                    }
                } while (projectedFreeCapacity > Config.MinFreeVmssCapacity);

                instancesNeeded++; // At this point projectedFreeCapacity is below the needed load, so we need to add one instance.
            }
            return instancesNeeded;
        }

        /// <summary>
        /// 
        /// </summary>
        private async void Scale()
        { 
            lock(_updatingLock)
            {
                if (_updating == false) _updating = true;
                else return;
            }

            var neededInstanceCount = CalculateNeededCapacity();
            var activeInstanceCount = _vmssInstances.Count(i => i.State == VmssInstanceState.Running);

            if (activeInstanceCount > neededInstanceCount)
            {
                // Close instances with the lowest load, so they don't accept new sessions.
                var instancesToDeallocate = _vmssInstances.OrderBy(i => i.Load).Take(activeInstanceCount - neededInstanceCount);
                foreach (var instance in instancesToDeallocate)
                {
                    instance.State = VmssInstanceState.Closing;
                    _vmssLoadManager.CloseInstance(instance);
                    instance.State = VmssInstanceState.Closed;
                }
            }
            else if (activeInstanceCount < neededInstanceCount)
            {
                // Open up any existing capacity that was not accepting new load to accept new load.
                var instancesToOpen = _vmssInstances.Where(i => i.State == VmssInstanceState.Closed).OrderBy(i => i.Load).Take(neededInstanceCount - activeInstanceCount);
                foreach (var instance in instancesToOpen)
                {
                    instance.State = VmssInstanceState.Opening;
                    _vmssLoadManager.OpenInstance(instance);
                    instance.State = VmssInstanceState.Running;
                    activeInstanceCount++;
                }
            }

            // Active and needed counts updated. Now check if the VMSS needs to be modified.
            if (activeInstanceCount < neededInstanceCount)
            {
                _vmssManager.AddInstancesAsync(neededInstanceCount - activeInstanceCount, UpdateCompleted);
            }
            else if (activeInstanceCount > neededInstanceCount)
            {
                var instancesToDelete = _vmssInstances.Where(i => i.State == VmssInstanceState.Closed && i.Load == 0).ToArray();
                _vmssManager.DeleteInstancesAsync(instancesToDelete.Select(i => i.InstanceId), UpdateCompleted);
                foreach (var instance in instancesToDelete)
                {
                    _vmssLoadManager.DeleteInstance(instance);
                    _vmssInstances.Remove(instance);
                }
            }
            else
            {
                UpdateCompleted();
            }
        }

        private void UpdateCompleted()
        {
            lock (_updatingLock)
            {
                _updating = false;
            }
        }

        /// <summary>
        /// Get the load from the <see cref="IVmssLoadManager"/> for all instances.
        /// </summary>
        private void UpdateLoad()
        {
            foreach(var instance in _vmssInstances)
            {
                instance.Load = _vmssLoadManager.GetLoad(instance);
            }
        }

        /// <summary>
        /// Check if there are any changes to the underlying VMSS, and update information about that if needed.
        /// </summary>
        private void UpdateVmssInfo()
        {
            var virtualMachines = _vmssManager.GetVirtualMachines();

            // Add any VMs that are not tracked in the capacity manager.
            foreach(var vm in virtualMachines)
            {
                var instance = _vmssInstances.SingleOrDefault(i => i.VmInfo.InstanceId == vm.InstanceId);
                if(instance == null)
                {
                    AddInstance(vm);
                }
            }

            // Check for any VMs that can no longer be used, and remove those.
            var inactiveInstances = new List<VmssInstanceInfo>();
            foreach(var instance in _vmssInstances)
            {
                var vm = virtualMachines.SingleOrDefault(v => v.InstanceId == instance.VmInfo.InstanceId);
                if(vm == null ||
                    vm.PowerState == PowerState.Deallocated ||
                    vm.PowerState == PowerState.Deallocating ||
                    vm.PowerState == PowerState.Stopped ||
                    vm.PowerState == PowerState.Stopping)
                {
                    // Instance either no longer exists or is (being taken) out of rotation
                    inactiveInstances.Add(instance);
                }
            }
            foreach(var instance in inactiveInstances)
            {
                DeleteInstance(instance);
            }
        }

        /// <summary>
        /// Add instance to internal tracking in the capacity manager, and notify the <see cref="IVmssLoadManager"/> about the new instance.
        /// </summary>
        /// <param name="vm">The VM to add as instance to track.</param>
        private void AddInstance(VmssVmInfo vm)
        {
            var instance = new VmssInstanceInfo(vm);
            _vmssInstances.Add(instance);
            _vmssLoadManager.AddInstance(instance);
        }

        /// <summary>
        /// Remove instance from internal tracking in the capacity manager, and notify the <see cref="IVmssLoadManager"/> the instance is no longer available.
        /// </summary>
        /// <param name="instance">Instance to delete.</param>
        private void DeleteInstance(VmssInstanceInfo instance)
        {
            _vmssLoadManager.DeleteInstance(instance);
            _vmssInstances.Remove(instance);
        }
    }
}
