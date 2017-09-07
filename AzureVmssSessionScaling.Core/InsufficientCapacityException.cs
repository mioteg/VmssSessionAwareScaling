using System;
using System.Collections.Generic;
using System.Text;

namespace AzureVmssSessionScaling.Core
{
    public class InsufficientCapacityException : Exception
    {
        public InsufficientCapacityException() : base()
        { }
        public InsufficientCapacityException(string message) : base(message)
        { }
        public InsufficientCapacityException(string message, Exception innerException) : base(message, innerException)
        { }
    }
}
