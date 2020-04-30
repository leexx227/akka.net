﻿//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2020 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2020 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Configuration;
using System.Diagnostics;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Routing;
using Akka.Configuration;
using Akka.Configuration.Hocon;
using Akka.Routing;
using Akka.Util.Internal;

namespace Samples.Cluster.RoundRobin
{
    class Program
    {
        private static Config _clusterConfig;

        private static int backendNum = Environment.ProcessorCount;
        private static string hostName = Environment.MachineName;

        public static int totalRequest = 1000000;

        public static Stopwatch sw;

        static async Task Main(string[] args)
        {
            var section = (AkkaConfigurationSection)ConfigurationManager.GetSection("akka");
            _clusterConfig = section.AkkaConfig;

            // backend
            //await StartBackend(args);

            // frontend
            await StartFrontend(args);

            Console.ReadKey();
        }

        static void LaunchBackend(string[] args)
        {
            var port = args.Length > 0 ? args[0] : "0";
            var config =
                    ConfigurationFactory.ParseString("akka.remote.dot-netty.tcp.port=" + port)
                    .WithFallback(ConfigurationFactory.ParseString("akka.cluster.roles = [backend]"))
                    .WithFallback(ConfigurationFactory.ParseString("akka.remote.dot-netty.tcp.hostname=" + hostName))
                        .WithFallback(_clusterConfig);

            var system = ActorSystem.Create("ClusterSystem", config);
            system.ActorOf(Props.Create<BackendActor>(), "backend");
        }

        static IActorRef GetFrontend(string[] args)
        {
            var port = args.Length > 0 ? args[0] : "0";
            var config =
                    ConfigurationFactory.ParseString("akka.remote.dot-netty.tcp.port=" + port)
                    .WithFallback(ConfigurationFactory.ParseString("akka.cluster.roles = [frontend]"))
                    .WithFallback(ConfigurationFactory.ParseString("akka.remote.dot-netty.tcp.hostname=" + hostName))
                        .WithFallback(_clusterConfig);

            var system = ActorSystem.Create("ClusterSystem", config);

            var ts = new TaskCompletionSource<bool>();
            //var backendRouter =
            //    system.ActorOf(
            //        Props.Empty.WithRouter(new ClusterRouterGroup(new RoundRobinGroup("/user/backend"),
            //            new ClusterRouterGroupSettings(10, ImmutableHashSet.Create("/user/backend"), false, "backend"))));

            var workers = new[] { "/user/backend" };
            var backendRouter =
                system.ActorOf(
                    Props.Empty.WithRouter(new ClusterRouterGroup(new RandomGroup(workers),
                        new ClusterRouterGroupSettings(1000, workers, true, "backend"))));
            var frontend = system.ActorOf(Props.Create(() => new FrontendActor(backendRouter)), "frontend");

            return frontend;

            //var interval = TimeSpan.FromSeconds(1);
            //var counter = new AtomicCounter();
            //system.Scheduler.Advanced.ScheduleRepeatedly(TimeSpan.FromSeconds(0), interval, () => frontend.Tell(new StartCommand("hello-" + counter.GetAndIncrement())));
        }

        static async Task StartBackend(string[] args)
        {
            int currentBackendNum = 0;

            if (args.Length >= 1)
            {
                backendNum = int.Parse(args[0]);
            }
            else
            {
                LaunchBackend(new[] { "2551" });
                LaunchBackend(new[] { "2552" });

                currentBackendNum = 2;
            }
            while (currentBackendNum < backendNum)
            {
                LaunchBackend(new string[0]);
                currentBackendNum++;
                Console.WriteLine($"Launch {currentBackendNum} backend actors.");
            }
        }

        static async Task StartFrontend(string[] args)
        {
            var client = GetFrontend(new string[0]);

            await Task.Delay(TimeSpan.FromSeconds(20));

            sw = Stopwatch.StartNew();

            for (int i = 0; i < totalRequest; i++)
            {
                client.Tell(new StartCommand("hello-" + i));
                //tasks.Add(client.Ask(new StartCommand("hello-" + i)).ContinueWith(r => Console.WriteLine($"Received: {r.Result}")));
            }
        }
    }
}

