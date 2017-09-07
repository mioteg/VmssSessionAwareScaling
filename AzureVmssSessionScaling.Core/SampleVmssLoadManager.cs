using System;
using System.Collections.Generic;
using System.Linq;

namespace AzureVmssSessionScaling.Core
{
    /// <summary>
    /// Sample implementation of <see cref="IVmssLoadManager"/>.
    /// This implementation is a simple session based manager. A fixed number of sessions can be added to the load of a VM.
    /// </summary>
    public class SampleVmssLoadManager : IVmssLoadManager
    {
        private List<SampleInstanceLoad> _instances = new List<SampleInstanceLoad>();
        private Dictionary<Guid, string> _sessions = new Dictionary<Guid, string>();

        public Tuple<Guid, string> RequestSession()
        {
            Guid guid = Guid.NewGuid();

            var instance = _instances.Where(i => i.AcceptingSessions && i.Sessions.Count < Config.MaxLoadPerInstance).OrderByDescending(i => i.Sessions.Count).FirstOrDefault();
            if(instance == null)
            {
                throw new InsufficientCapacityException("No instances available.");
            }
            instance.Sessions.Add(guid);
            _sessions.Add(guid, instance.Id);

            return new Tuple<Guid, string>(guid, instance.Id);
        }

        public void ReleaseSession(Guid sessionId)
        {
            var instanceId = _sessions[sessionId];
            _instances.Single(i => i.Id == instanceId).Sessions.Remove(sessionId);
            _sessions.Remove(sessionId);
        }

        public void AddInstance(VmssInstanceInfo instance)
        {
            
            _instances.Add(new SampleInstanceLoad(instance.InstanceId));
        }

        public int GetLoad(VmssInstanceInfo instance)
        {
            var node = _instances.SingleOrDefault(i => i.Id == instance.InstanceId);
            if (node == null) return 0; // If the instance is not known yet in the load manager, there is no load yet.
            return node.Sessions.Count;
        }

        public void DeleteInstance(VmssInstanceInfo instance)
        {
            _instances.Remove(_instances.Single(i => i.Id == instance.InstanceId));
        }

        public void CloseInstance(VmssInstanceInfo instance)
        {
            _instances.Single(i => i.Id == instance.InstanceId).AcceptingSessions = false;
        }

        public void OpenInstance(VmssInstanceInfo instance)
        {
            _instances.Single(i => i.Id == instance.InstanceId).AcceptingSessions = true;
        }
    }
}
