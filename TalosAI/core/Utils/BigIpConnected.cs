//using System;
//using System.Collections.Generic;
//using System.Net;
//using iControl;

//namespace TalosAI.core.Utils
//{
//    class BigIpConnected
//    {
//        private static int MIN_ARGS = 3;
//        private static string USAGE =
//    "ManagementEMGetTaskStatus    " +
//    "[]";
//        private static int EM_PORT = 443;


//        /**
//         * The main method.
//         *
//         * @param args command line arguments
//         */

//        static void Main(string[] args)
//        {

//            if (args.Length < MIN_ARGS)
//            {
//                Console.WriteLine("Usage: " + USAGE);
//                Environment.Exit(1);
//            }

//            string emAddress = args[0];
//            string emUsername = args[1];
//            string emPassword = args[2];

//            String[] taskIds = new String[args.Length - MIN_ARGS];

//            for (int i = 0; i < taskIds.Length; i++)
//            {
//                taskIds[i] = args[i + MIN_ARGS];
//            }

//            iControl.ManagementEMTaskStatus[] statuses = null;

//            try
//            {
//                Interfaces ic = new Interfaces();
//                ic.initialize(emAddress, EM_PORT, emUsername, emPassword);

//                statuses = ic.ManagementEM.get_task_status(taskIds);

//                if (statuses.Length != taskIds.Length)
//                {
//                    throw new Exception("wrong number of status values returned");
//                }
//            }
//            catch (Exception e)
//            {
//                Console.WriteLine("Failed to get task status: " + e.Message);
//                Environment.Exit(1);
//            }

//            for (int i = 0; i < taskIds.Length; i++)
//            {
//                Console.Write("Task ");
//                Console.Write(taskIds[i]);
//                Console.Write(": ");
//                Console.WriteLine(statuses[i]);
//            }

//        } // Main

//    } // class ManagementEMGetTaskStatus
//}