using AzureVmssSessionScaling.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AzureVmssSessionScaling.TestConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            //var _vmssManager = new TestVmssManager();
            var _vmssManager = new VmssManager();
            var _vmssLoadManager = new SampleVmssLoadManager();
            var _vmssCapacityManager = new VmssCapacityManager(_vmssManager, _vmssLoadManager);
            var sessionList = new Dictionary<int, Tuple<Guid,string>>();
            int sessionCount = 1;
            
            while (true)
            {
                Console.Write("COMMAND? ");

                var input = Console.ReadLine();
                if (input.ToLower().Equals("exit")) break;

                if (input.ToLower().Equals("add"))
                {
                    try
                    {
                        sessionList.Add(sessionCount, _vmssLoadManager.RequestSession());
                        sessionCount++;
                    }
                    catch (InsufficientCapacityException)
                    {
                        Console.WriteLine("No capacity to add session.");
                    }
                }
                else if (input.ToLower().StartsWith("remove "))
                {
                    int param;
                    if (int.TryParse(input.ToLower().Substring("remove ".Length), out param))
                    {
                        if (sessionList.ContainsKey(param))
                        {
                            _vmssLoadManager.ReleaseSession(sessionList[param].Item1);
                            sessionList.Remove(param);
                        }
                        else Console.WriteLine("Unknown session.");
                    }
                    else
                    {
                        Console.WriteLine("Not a valid session.");
                    }
                }
                else
                {
                    Console.WriteLine("Unknown command");
                }

                _vmssCapacityManager.UpdateVmss();

                Console.WriteLine();
                Console.WriteLine("SESSIONS");
                foreach(var session in sessionList)
                {
                    Console.WriteLine("{0}. {1} - {2}", session.Key, session.Value.Item2, session.Value.Item1);
                }
            }
        }
    }
}
