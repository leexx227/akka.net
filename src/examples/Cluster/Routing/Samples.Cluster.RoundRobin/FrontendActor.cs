//-----------------------------------------------------------------------
// <copyright file="FrontendActor.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2020 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2020 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster;

namespace Samples.Cluster.RoundRobin
{
    public class FrontendActor : UntypedActor, IWithUnboundedStash
    {
        protected readonly IActorRef BackendRouter;
        protected int jobCount = 0;

        private readonly TaskCompletionSource<bool> jobCompletion;
        private int maxJobs;

        public FrontendActor(IActorRef backendRouter, int totalJobs, TaskCompletionSource<bool> completion)
        {
            BackendRouter = backendRouter;
            maxJobs = totalJobs;
            jobCompletion = completion;
        }

        protected Akka.Cluster.Cluster Cluster = Akka.Cluster.Cluster.Get(Context.System);

        /// <summary>
        /// Need to subscribe to cluster changes
        /// </summary>
        protected override void PreStart()
        {
            Cluster.Subscribe(Self, new[] { typeof(ClusterEvent.MemberUp) });
        }

        /// <summary>
        /// Re-subscribe on restart
        /// </summary>
        protected override void PostStop()
        {
            Cluster.Unsubscribe(Self);
        }

        protected override void OnReceive(object message)
        {
            if (message is ClusterEvent.MemberUp)
            {
                Console.WriteLine("Frontend [{0}]: Cluster is ready. Able to begin jobs.");
                //ready to begin routing messages to back-end
                Become(ReadyToProcess);
                Stash.UnstashAll();
            }
            else
            {
                Stash.Stash();
            }
        }

        protected void ReadyToProcess(object message)
        {
            if (message is StartCommand)
            {
                var sc = message as StartCommand;
                BackendRouter.Tell(new FrontendCommand()
                {
                    Message = string.Format("message {0}", jobCount),
                    JobId = sc.CommandText
                });
            }
            else if (message is CommandComplete)
            {
                jobCount++;
                Console.WriteLine("Frontend [{0}]: Received {1} CommandComplete from {2}", Cluster.SelfAddress, jobCount, Sender);
                if (jobCount >= maxJobs)
                {
                    Console.WriteLine("Frontend [{0}]: Finish all the {1} jobs", Cluster.SelfAddress, jobCount);
                    jobCompletion.TrySetResult(true);
                    Context.Stop(Self);
                }
            }
        }

        public IStash Stash { get; set; }
    }
}

