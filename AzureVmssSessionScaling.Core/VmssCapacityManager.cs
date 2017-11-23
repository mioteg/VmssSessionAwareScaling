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
        /// Closes instances based on the number of instances that need to be closed.
        /// Takes into account the fault domains of the different VMs to ensure an equal distribution
        /// across Fault Domains.
        /// </summary>
        /// <param name="numberOfInstancesToClose">Number of instances that need to be closed.</param>
        /// <returns>Number of closed instances.</returns>
        private int CloseInstances(int numberOfInstancesToClose)
        {
            int closedInstances = 0;
            for (var i = 0; i < numberOfInstancesToClose; i++)
            {
                var activeInstances = _vmssInstances.Where(v => v.State == VmssInstanceState.Running).ToList();
                var mostVmsInFaultDomain = activeInstances.GroupBy(v => v.VmInfo.FaultDomain).OrderByDescending(g => g.Count()).Select(g => g.Count()).First(); 
                var faultDomainsWithMostVms = activeInstances.GroupBy(v => v.VmInfo.FaultDomain).Where(g => g.Count() == mostVmsInFaultDomain).Select(g => g.Key);
                var instanceToClose = activeInstances.Where(v => faultDomainsWithMostVms.Contains(v.VmInfo.FaultDomain)).OrderBy(o => o.Load).FirstOrDefault();
                if(instanceToClose != null)
                {
                    instanceToClose.State = VmssInstanceState.Closing;
                    _vmssLoadManager.CloseInstance(instanceToClose);
                    instanceToClose.State = VmssInstanceState.Closed;
                    closedInstances++;
                }
            }
            return closedInstances;
        }

        /// <summary>
        /// Opens instances based on the number of instances that need to be opened.
        /// Takes into account the fault domains of the different VMs to ensure an equal distribution
        /// across Fault Domains.
        /// </summary>
        /// <param name="numberOfInstancesToOpen">Number of instances that need to be opened.</param>
        /// <returns>Number of instances opened.</returns>
        private int OpenInstances(int numberOfInstancesToOpen)
        {
            int openedInstances = 0;
            var inactiveInstances = _vmssInstances.Where(v => v.State == VmssInstanceState.Closed).ToList();
            if (numberOfInstancesToOpen >= inactiveInstances.Count)
            {
                foreach (var instance in inactiveInstances)
                {
                    instance.State = VmssInstanceState.Opening;
                    _vmssLoadManager.OpenInstance(instance);
                    instance.State = VmssInstanceState.Running;
                    openedInstances++;
                }
            }
            else
            {
                for (var i = 0; i < numberOfInstancesToOpen; i++)
                {
                    var mostInactiveVmsInFaultDomain = inactiveInstances.GroupBy(v => v.VmInfo.FaultDomain).OrderByDescending(g => g.Count()).Select(g => g.Count()).First();
                    var faultDomainsWithMostInactiveVms = inactiveInstances.GroupBy(v => v.VmInfo.FaultDomain).Where(g => g.Count() == mostInactiveVmsInFaultDomain).Select(g => g.Key);
                    var instanceToOpen = inactiveInstances.Where(v => faultDomainsWithMostInactiveVms.Contains(v.VmInfo.FaultDomain)).OrderBy(o => o.Load).FirstOrDefault();
                    if (instanceToOpen != null)
                    {
                        instanceToOpen.State = VmssInstanceState.Opening;
                        _vmssLoadManager.OpenInstance(instanceToOpen);
                        instanceToOpen.State = VmssInstanceState.Running;
                        openedInstances++;
                        inactiveInstances.Remove(instanceToOpen);
                    }
                }
            }
            return openedInstances;
        }

        /// <summary>
        /// Determines whether the VMSS needs to scale up or down. If there is overcapacity instances
        /// are marked to be closed (no longer accept load). Closed instances with no load are removed.
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
                activeInstanceCount += CloseInstances(activeInstanceCount - neededInstanceCount);
            }
            else if (activeInstanceCount < neededInstanceCount)
            {
                // Open up any existing capacity that was not accepting new load to accept new load.
                activeInstanceCount += OpenInstances(neededInstanceCount - activeInstanceCount);
            }

            // Active and needed counts updated. Now check if the VMSS needs to be modified.
            if (activeInstanceCount < neededInstanceCount)
            {
                _vmssManager.AddInstancesAsync(neededInstanceCount - activeInstanceCount, UpdateCompleted);
            }
            else
            {
                var instancesToDelete = _vmssInstances.Where(i => i.State == VmssInstanceState.Closed && i.Load == 0).ToArray();
                _vmssManager.DeleteInstancesAsync(instancesToDelete.Select(i => i.InstanceId), UpdateCompleted);
                foreach (var instance in instancesToDelete)
                {
                    _vmssLoadManager.DeleteInstance(instance);
                    _vmssInstances.Remove(instance);
                }
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
