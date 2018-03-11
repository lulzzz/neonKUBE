﻿//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;

using Neon.Cluster;
using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Docker;
using Neon.Net;

namespace NeonDnsHealth
{
    /// <summary>
    /// Implements the <b>neon-dns-health</b> service.  See 
    /// <a href="https://hub.docker.com/r/neoncluster/neon-dns-health/">neoncluster/neon-dns-health</a>
    /// for more information.
    /// </summary>
    public static class Program
    {
        private const string serviceName = "neon-dns-health";

        private static ProcessTerminator    terminator;
        private static INeonLogger          log;
        private static ConsulClient         consul;
        private static DockerClient         docker;
        private static TimeSpan             consulPollInterval;

        /// <summary>
        /// Application entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static async Task Main(string[] args)
        {
            LogManager.Default.SetLogLevel(Environment.GetEnvironmentVariable("LOG_LEVEL"));
            log = LogManager.Default.GetLogger(typeof(Program));
            log.LogInfo(() => $"Starting [{serviceName}:{Program.GitVersion}]");
            log.LogInfo(() => $"LOG_LEVEL={LogManager.Default.LogLevel.ToString().ToUpper()}");

            // Create process terminator that handles process termination signals.

            terminator = new ProcessTerminator(log);

            try
            {
                // Establish the cluster connections.

                if (NeonHelper.IsDevWorkstation)
                {
                    NeonClusterHelper.OpenRemoteCluster();
                }
                else
                {
                    NeonClusterHelper.OpenCluster();
                }

                // Ensure that we're running on a manager node.  We won't be able
                // to query swarm status otherwise.

                var nodeRole = Environment.GetEnvironmentVariable("NEON_NODE_ROLE");

                if (string.IsNullOrEmpty(nodeRole))
                {
                    log.LogCritical(() => "Container does not appear to be running on a neonCLUSTER.");
                    Program.Exit(1);
                }

                if (!string.Equals(nodeRole, NodeRole.Manager, StringComparison.OrdinalIgnoreCase))
                {
                    log.LogCritical(() => $"[neon-dns-health] service is running on a [{nodeRole}] cluster node.  Running on only [{NodeRole.Manager}] nodes are supported.");
                    Program.Exit(1);
                }

                // Open the cluster data services and then start the main service task.

                log.LogDebug(() => $"Opening Consul");

                using (consul = NeonClusterHelper.OpenConsul())
                {
                    log.LogDebug(() => $"Opening Docker");

                    using (docker = NeonClusterHelper.OpenDocker())
                    {
                        await RunAsync();
                    }
                }
            }
            catch (Exception e)
            {
                log.LogCritical(e);
                Program.Exit(1);
            }
            finally
            {
                NeonClusterHelper.CloseCluster();
                terminator.ReadyToExit();
            }

            Program.Exit(0);
        }

        /// <summary>
        /// Returns the program version as the Git branch and commit and an optional
        /// indication of whether the program was build from a dirty branch.
        /// </summary>
        public static string GitVersion
        {
            get
            {
                var version = $"{ThisAssembly.Git.Branch}-{ThisAssembly.Git.Commit}";

#pragma warning disable 162 // Unreachable code

                if (ThisAssembly.Git.IsDirty)
                {
                    version += "-DIRTY";
                }

#pragma warning restore 162 // Unreachable code

                return version;
            }
        }

        /// <summary>
        /// Exits the service with an exit code.
        /// </summary>
        /// <param name="exitCode">The exit code.</param>
        public static void Exit(int exitCode)
        {
            log.LogInfo(() => $"Exiting: [{serviceName}]");
            terminator.ReadyToExit();
            Environment.Exit(exitCode);
        }

        /// <summary>
        /// Implements the service as a <see cref="Task"/>.
        /// </summary>
        /// <returns>The <see cref="Task"/>.</returns>
        private static async Task RunAsync()
        {
            // Launch the sub-tasks.  These will run until the service is terminated.

            var tasks = new List<Task>();

            // Wait for all tasks to exit cleanly for a normal shutdown.

            await NeonHelper.WaitAllAsync(tasks);

            terminator.ReadyToExit();
        }
    }
}