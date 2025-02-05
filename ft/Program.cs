﻿using ft.CLI;
using ft.Listeners;
using ft.Streams;
using ft.Utilities;
using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using ft.Tunnels;

namespace ft
{
    public class Program
    {
        const string PROGRAM_NAME = "File Tunnel";
        const string VERSION = "2.2.4";

        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Options))]
        public static void Main(string[] args)
        {
            Log($"{PROGRAM_NAME} {VERSION}");

            var parser = new Parser(settings =>
            {
                settings.AllowMultiInstance = true;
                settings.AutoHelp = true;
                settings.HelpWriter = Console.Out;
                settings.AutoVersion = true;
            });

            parser.ParseArguments<Options>(args)
               .WithParsed(o =>
               {
                   var localListeners = new MultiServer();
                   localListeners.Add("tcp", o.LocalTcpForwards, false);
                   localListeners.Add("udp", o.LocalUdpForwards, false);

                   if (Path.GetFullPath(o.ReadFrom).Contains("thinclient_drives") && !o.IsolatedReads)
                   {
                       Log($"Warning: It appears the Read file is stored in xrdp's Drive Redirection folder.", ConsoleColor.Yellow);
                       Log($"This can result in the File Tunnel not achieving synchronisation.", ConsoleColor.Yellow);
                       Log($"Recommendation: Run File Tunnel using an extra arg --isolated-reads", ConsoleColor.Yellow);
                       Log($"Continuing.", ConsoleColor.Yellow);
                   }

                   var sharedFileManager = new SharedFileManager(
                                                    o.ReadFrom.Trim(),
                                                    o.WriteTo.Trim(),
                                                    o.PurgeSizeInBytes,
                                                    o.TunnelTimeoutMilliseconds,
                                                    o.IsolatedReads,
                                                    o.Verbose);

                   var localToRemoteTunnel = new LocalToRemoteTunnel(localListeners, sharedFileManager, o.PurgeSizeInBytes, o.ReadDurationMillis);
                   var remoteToLocalTunnel = new RemoteToLocalTunnel(
                                                    o.RemoteTcpForwards.ToList(),
                                                    o.RemoteUdpForwards.ToList(),
                                                    sharedFileManager,
                                                    localToRemoteTunnel,
                                                    o.PurgeSizeInBytes,
                                                    o.ReadDurationMillis,
                                                    o.UdpSendFrom);

                   sharedFileManager.Start();
               })
               .WithNotParsed(o =>
               {
                   Environment.Exit(1);
               });

            while (true)
            {
                try
                {
                    Thread.Sleep(1000);
                }
                catch
                {
                    break;
                }
            }
        }

        public static readonly ConsoleColor OriginalConsoleColour = Console.ForegroundColor;
        public static readonly Random Random = new();

        public static readonly object ConsoleOutputLock = new();

        public static void Log(string str, ConsoleColor? color = null)
        {
            lock (ConsoleOutputLock)
            {
                // Change color if specified
                if (color.HasValue)
                {
                    Console.ForegroundColor = color.Value;
                }

                Console.WriteLine($"{DateTime.Now}: {str}");

                // Reset to original color
                Console.ForegroundColor = OriginalConsoleColour;
            }
        }
    }
}
