using System;
using System.Collections.Generic;
using System.Text;

namespace AzureVmssSessionScaling.Core
{
    public interface IVmssLoadManager
    {
        int GetLoad(VmssInstanceInfo instance);

        void AddInstance(VmssInstanceInfo instance);

        void DeleteInstance(VmssInstanceInfo instance);

        void CloseInstance(VmssInstanceInfo instance);

        void OpenInstance(VmssInstanceInfo instance);
    }
}
