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
            var _vmssManager = new TestVmssManager();
            //var _vmssManager = new VmssManager();
            var _vmssLoadManager = new SampleVmssLoadManager();
            var _vmssCapacityManager = new VmssCapacityManager(_vmssManager, _vmssLoadManager);
            var sessionList = new Dictionary<int, Tuple<Guid,string>>();
            int sessionCount = 1;

            // Pre load some sessions, and remove to simulate some sessions that have dropped.
            for (var i = 0; i < 21; i++)
            {
                sessionList.Add(sessionCount, _vmssLoadManager.RequestSession());
                sessionCount++;
                _vmssCapacityManager.UpdateVmss();
            }
            RemoveSession(_vmssLoadManager, _vmssCapacityManager, sessionList, 1);
            RemoveSession(_vmssLoadManager, _vmssCapacityManager, sessionList, 2);
            RemoveSession(_vmssLoadManager, _vmssCapacityManager, sessionList, 3);
            RemoveSession(_vmssLoadManager, _vmssCapacityManager, sessionList, 6);
            RemoveSession(_vmssLoadManager, _vmssCapacityManager, sessionList, 7);
            RemoveSession(_vmssLoadManager, _vmssCapacityManager, sessionList, 8);
            RemoveSession(_vmssLoadManager, _vmssCapacityManager, sessionList, 9);
            OutputSessions(sessionList);
            
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
                            RemoveSession(_vmssLoadManager, _vmssCapacityManager, sessionList, param);
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

                
                OutputSessions(sessionList);
            }
        }

        private static void RemoveSession(SampleVmssLoadManager vmssLoadManager, VmssCapacityManager vmssCapacityManager,  Dictionary<int, Tuple<Guid, string>> sessionList, int param)
        {
            vmssLoadManager.ReleaseSession(sessionList[param].Item1);
            sessionList.Remove(param);
            vmssCapacityManager.UpdateVmss();
        }

        private static void OutputSessions(Dictionary<int, Tuple<Guid, string>> sessionList)
        {
            Console.WriteLine();
            Console.WriteLine("SESSIONS");
            foreach (var session in sessionList)
            {
                Console.WriteLine("{0}. {1} - {2}", session.Key, session.Value.Item2, session.Value.Item1);
            }
        }
    }
}
