﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using DasMulli.Win32.ServiceUtils;

namespace FoxyLink
{
    public class Host
    {
        private const string RunAsServiceFlag = "--run-as-service";
        private const string RegisterServiceFlag = "--register-service";
        private const string UnregisterServiceFlag = "--unregister-service";
        private const string InteractiveFlag = "--interactive";

        public static void Main(string[] args)
        {
            GlobalConfiguration.Configuration.UseConfiguration();

            try
            {
                if (args.Contains(RunAsServiceFlag))
                {
                    RunAsService(args);
                }
                else if (args.Contains(RegisterServiceFlag))
                {
                    RegisterService();
                }
                else if (args.Contains(UnregisterServiceFlag))
                {
                    UnregisterService();
                }
                else if (args.Contains(InteractiveFlag))
                {
                    RunInteractive(args);
                }
                else
                {
                    DisplayHelp();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error ocurred: {ex.Message}");
            }
        }

        private static void RunAsService(string[] args)
        {
            var testService = new Service(args.Where(a =>
                a != InteractiveFlag).ToArray());
            var serviceHost = new Win32ServiceHost(testService);
            serviceHost.Run();
        }

        private static void RunInteractive(string[] args)
        {
            var testService = new Service(args.Where(a =>
                a != InteractiveFlag).ToArray());
            testService.Start(new string[0], () => { });
            Console.WriteLine("Running interactively, press enter to stop.");
            Console.ReadLine();
            testService.Stop();
        }

        private static void RegisterService()
        {
            // Environment.GetCommandLineArgs() includes the current DLL from a
            // "dotnet my.dll --register-service" call, which is not passed 
            // to Main()
            var remainingArgs = Environment.GetCommandLineArgs()
                .Where(arg => arg != RegisterServiceFlag)
                .Select(EscapeCommandLineArgument)
                .Append(RunAsServiceFlag);

            var host = Process.GetCurrentProcess().MainModule.FileName;

            if (!host.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            {
                // For self-contained apps, skip the dll path
                remainingArgs = remainingArgs.Skip(1);
            }

            var fullServiceCommand = host + " " + string.Join(" ", remainingArgs);

            // Do not use LocalSystem in production.. but this is good for 
            // demos as LocalSystem will have access to some random git-clone path
            // Note that when the service is already registered and running, 
            // it will be reconfigured but not restarted
            var serviceDefinition = new ServiceDefinitionBuilder(Configuration.Current["HostData:ServiceName"])
                .WithDisplayName(Configuration.Current["HostData:ServiceDisplayName"])
                .WithDescription(Configuration.Current["HostData:ServiceDescription"])
                .WithBinaryPath(fullServiceCommand)
                .WithCredentials(Win32ServiceCredentials.LocalSystem)
                .WithAutoStart(true)
                .Build();

            new Win32ServiceManager().CreateOrUpdateService(serviceDefinition, startImmediately: true);

            Console.WriteLine($@"Successfully registered and started service ""{Configuration.Current["HostData:ServiceDisplayName"]}"" (""{Configuration.Current["HostData:ServiceDescription"]}"")");
        }

        private static void UnregisterService()
        {
            new Win32ServiceManager()
                .DeleteService(Configuration.Current["HostData:ServiceName"]);

            Console.WriteLine($@"Successfully unregistered service ""{Configuration.Current["HostData:ServiceDisplayName"]}"" (""{Configuration.Current["HostData:ServiceDescription"]}"")");
        }

        private static void DisplayHelp()
        {
            Console.WriteLine(Configuration.Current["HostData:ServiceDescription"]);
            Console.WriteLine();
            Console.WriteLine("This demo application is intened to be run as windows service. Use one of the following options:");
            Console.WriteLine("  --register-service        Registers and starts this program as a windows service named \"" + Configuration.Current["HostData:ServiceDisplayName"] + "\"");
            Console.WriteLine("                            All additional arguments will be passed to ASP.NET Core's WebHostBuilder.");
            Console.WriteLine("  --unregister-service      Removes the windows service creatd by --register-service.");
            Console.WriteLine("  --interactive             Runs the underlying asp.net core app. Useful to test arguments.");
        }

        private static string EscapeCommandLineArgument(string arg)
        {
            // http://stackoverflow.com/a/6040946/784387
            arg = Regex.Replace(arg, @"(\\*)" + "\"", @"$1$1\" + "\"");
            arg = "\"" + Regex.Replace(arg, @"(\\+)$", @"$1$1") + "\"";
            return arg;
        }
    }
}