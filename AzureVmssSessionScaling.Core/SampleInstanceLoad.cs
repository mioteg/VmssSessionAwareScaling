using System;
using System.Collections.Generic;

namespace AzureVmssSessionScaling.Core
{
    /// <summary>
    /// Class for the <see cref="SampleVmssLoadManager"/> to track the load on an instance.
    /// </summary>
    public class SampleInstanceLoad
    {
        public SampleInstanceLoad(string id)
        {
            Id = id;
            AcceptingSessions = true;
            Sessions = new List<Guid>();
        }

        public string Id { get; private set; }

        public List<Guid> Sessions { get; set; }

        public bool AcceptingSessions { get; set; }

    }
}
