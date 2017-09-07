using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AzureVmssSessionScaling.Core
{
    public interface IVmssManager
    {
        Task AddInstancesAsync(int count, Action callback);
        Task DeleteInstancesAsync(IEnumerable<string> instanceIds, Action callback);
        IEnumerable<VmssVmInfo> GetVirtualMachines();
    }
}