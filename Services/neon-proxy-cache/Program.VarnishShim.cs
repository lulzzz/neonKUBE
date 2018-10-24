﻿//-----------------------------------------------------------------------------
// FILE:	    Program.VarnishShim.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Docker;
using Neon.Hive;
using Neon.HiveMQ;
using Neon.IO;
using Neon.Tasks;
using Neon.Time;

namespace NeonProxyCache
{
    public static partial class Program
    {
        // We're going to pass this directory to [varnishd] as the working
        // directory using the -n] option.  Normally, Varnish would put its
        // files in an instance subdirectory named with an instance ID.  We need
        // to have Docker map in a TMPFS for the [_.vsm_mgt] directory so the
        // shared memory log files won't actually do any I/O (which would be 
        // really bad).  Having a generated instance ID will prevent us from
        // knowing where to mount the TMPFS in advance.
        //
        // Fortunately, the [-n] option allows us to specify exactly where
        // the working directory will be located without an instance ID.
        //
        // I would have rather just mounted the TMPFS to [/var/lib/varnish]
        // but that doesn't work because Docker doesn't currently (as of 10/23/2018)
        // have a way to specify EXEC for a TMPFS mount for Swarm services:
        //
        //      https://github.com/moby/moby/pull/36720

        private const string workDir = "/var/lib/varnish";

        private const string NotDeployedHash = "NOT-DEPLOYED";
        private const string VclProgram      = "main";
        private const string AdminInterface  = "127.0.0.1:2000";

        private static AsyncMutex               asyncLock    = new AsyncMutex();
        private static string                   deployedHash = NotDeployedHash;
        private static BroadcastChannel         proxyNotifyChannel;

        /// <summary>
        /// Retrieves the IDs of the currently running Varnish processes.
        /// </summary>
        /// <returns>The list of Varnish processes IDs.</returns>
        private static List<int> GetVarnishProcessIds()
        {
            var processes = Process.GetProcessesByName("varnishd").ToList();
            var ids       = new List<int>();

            foreach (var process in processes)
            {
                ids.Add(process.Id);
                process.Dispose();
            }

            return ids;
        }

        /// <summary>
        /// Kills the oldest process from a list of process IDs.
        /// </summary>
        /// <param name="processIDs">The list of processes IDs.</param>
        private static void KillOldestProcess(List<int> processIDs)
        {
            if (processIDs.Count == 0)
            {
                return;
            }

            var processes = new List<Process>();

            try
            {
                foreach (var processId in processIDs)
                {
                    try
                    {
                        processes.Add(Process.GetProcessById(processId));
                    }
                    catch
                    {
                        // Intentionally ignoring processes that no longer exist.
                    }
                }

                processes.OrderBy(p => p.StartTime).First().Kill();
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }

        /// <summary>
        /// Kills a process based on its process ID.
        /// </summary>
        /// <param name="id">The process ID.</param>
        private static void KillProcess(int id)
        {
            try
            {
                var process = Process.GetProcessById(id);

                if (process != null)
                {
                    process.Kill();
                    process.Dispose();
                }
            }
            catch
            {
                // Intentionally ignored.
            }
        }

        /// <summary>
        /// Manages the Varnish initial configuration from Consul and Vault settings and
        /// then listens for <see cref="ProxyUpdateMessage"/> messages on the <see cref="HiveMQChannels.ProxyNotify"/>
        /// broadcast by <b>neon-proxy-manager</b> signalling that the configuration has been
        /// updated.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method will terminate the service with an error if the configuration could 
        /// not be retrieved or applied for the first attempt since this very likely indicates
        /// a larger problem with the hive (e.g. Consul is down).
        /// </para>
        /// <para>
        /// If Varnish was configured successfully on the first attempt, subsequent failures
        /// will be logged as warnings but the service will continue running with the out-of-date
        /// configuration to provide some resilience for running hive services.
        /// </para>
        /// </remarks>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async static Task VarnishShim()
        {
            // This call ensures that Varnish is started immediately.

            await ConfigureVarnish();

            // Register a handler for [ProxyUpdateMessage] messages that determines
            // whether the message is meant for this service instance and handle it.

            StartNotifyHandler();

            // Register an event handler that will be fired when the HiveMQ bootstrap
            // settings change.  This will restart the [ProxyUpdateMessage] listener
            // using the new settings.

            hive.HiveMQ.Internal.HiveMQBootstrapChanged +=
                (s, a) =>
                {
                    StartNotifyHandler();
                };

            // Spin quietly while waiting for a cancellation indicating that
            // the service is stopping.

            var task = new AsyncPeriodicTask(
                TimeSpan.FromMinutes(5),
                onTaskAsync: async () => await Task.FromResult(false),
                onTerminateAsync:
                    async () =>
                    {
                        log.LogInfo(() => "VARNISH-SHIM: Terminating");

                        if (proxyNotifyChannel != null)
                        {
                            proxyNotifyChannel.Dispose();
                            proxyNotifyChannel = null;
                        }

                        await Task.CompletedTask;
                    },
                cancellationTokenSource: terminator.CancellationTokenSource);

            await task.Run();
        }

        /// <summary>
        /// Starts or restarts the handler listening for the [ProxyUpdateMessage] messages.
        /// </summary>
        private static void StartNotifyHandler()
        {
            lock (syncLock)
            {
                // Use the latest settings to reconnect to the [proxy-notify] channel.

                if (proxyNotifyChannel != null)
                {
                    proxyNotifyChannel.Dispose();
                }

                proxyNotifyChannel = hive.HiveMQ.Internal.GetProxyNotifyChannel(useBootstrap: true).Open();

                // Register a handler for [ProxyUpdateMessage] messages that determines
                // whether the message is meant for this service instance and handle it.

                proxyNotifyChannel.ConsumeAsync<ProxyUpdateMessage>(
                    async message =>
                    {
                        // We cannot process updates in parallel so we'll use an 
                        // AsyncMutex to prevent this.

                        using (await asyncLock.AcquireAsync())
                        {
                            var forThisInstance = false;

                            if (isPublic)
                            {
                                forThisInstance = message.PublicProxy;
                            }
                            else
                            {
                                forThisInstance = message.PrivateProxy;
                            }

                            if (!forThisInstance)
                            {
                                log.LogInfo(() => $"VARNISH-SHIM: Received but ignorning: {message}");
                                return;
                            }

                            log.LogInfo(() => $"VARNISH-SHIM: Received: {message}");
                            await ConfigureVarnish();
                        }
                    });
            }
        }

        /// <summary>
        /// Configures Varnish based on the current load balancer configuration.
        /// </summary>
        /// <remarks>
        /// This method will terminate the service if Varnish could not be started
        /// for the first call.
        /// </remarks>
        public async static Task ConfigureVarnish()
        {
            try
            {
                // Retrieve the configuration HASH and compare that with what 
                // we have already deployed.

                log.LogInfo(() => $"VARNISH-SHIM: Retrieving configuration HASH from Consul path [{configHashKey}].");

                string configHash;

                try
                {
                    configHash = await consul.KV.GetString(configHashKey, terminator.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    SetErrorTime();
                    log.LogError($"VARNISH-SHIM: Cannot retrieve [{configHashKey}] from Consul.", e);
                    return;
                }

                if (configHash == deployedHash)
                {
                    log.LogInfo(() => $"VARNISH-SHIM: Configuration with [hash={configHash}] is already deployed.");
                    return;
                }
                else
                {
                    log.LogInfo(() => $"VARNISH-SHIM: Configuration hash has changed from [{deployedHash}] to [{configHash}].");
                }

                // Download the configuration archive from Consul and extract it to
                // the new configuration directory (after ensuring that the directory
                // has been cleared).

                log.LogInfo(() => $"VARNISH-SHIM: Retrieving configuration ZIP archive from Consul path [{configKey}].");

                byte[] zipBytes;

                try
                {
                    zipBytes = await consul.KV.GetBytes(configKey, terminator.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    SetErrorTime();
                    log.LogError($"VARNISH-SHIM: Cannot retrieve [{configKey}] from Consul.", e);
                    return;
                }

                if (configHash == deployedHash)
                {
                    log.LogInfo(() => $"VARNISH-SHIM: Configuration with [hash={configHash}] is already deployed.");
                    return;
                }

                var zipPath = Path.Combine(configUpdateFolder, "haproxy.zip");

                log.LogInfo(() => $"VARNISH-SHIM: Extracting ZIP archive to [{configUpdateFolder}].");

                // Ensure that we have a fresh update folder.

                NeonHelper.DeleteFolder(configUpdateFolder);
                Directory.CreateDirectory(configUpdateFolder);

                // Unzip the configuration archive to the update folder.

                File.WriteAllBytes(zipPath, zipBytes);

                var response = NeonHelper.ExecuteCapture("unzip",
                    new object[]
                    {
                        "-o", zipPath,
                        "-d", configUpdateFolder
                    });

                response.EnsureSuccess();

                // It's possible that very old versions of [neon-proxy-manager] haven't
                // included a generated [varnish.vcl] file within the ZIP archive.  We'll
                // create a stub (do-nothing) file in this case to make Varnish happy.

                if (!File.Exists(configUpdatePath))
                {
                    const string stubVcl =
@"vcl 4.0;

# The proxy configuration archive did not include a [varnish.vcl] file so
# we're going to generate a stub VCL file that doesn't do anything.

import directors;

backend stub {
    .host = ""localhost"";
    .port = ""8080"";
}

sub vcl_init {
    new round_robin_director = directors.round_robin();
    round_robin_director.add_backend(stub);
}

sub vcl_recv {
    set req.backend_hint = round_robin_director.backend();
}
";
                    File.WriteAllText(configUpdatePath, NeonHelper.ToLinuxLineEndings(stubVcl));
                }

                // Verify the configuration.

                log.LogInfo(() => "Verifying Varnish configuration.");

                response = NeonHelper.ExecuteCapture("varnishd",
                    new object[]
                    {
                        "-C",
                        "-f", configUpdatePath,
                        "-n", workDir
                    });

                if (response.ExitCode == 0)
                {
                    log.LogInfo(() => "VARNISH-SHIM: Configuration is OK.");
                }
                else
                {
                    SetErrorTime();

                    // If Varnish is running then we'll let it continue using
                    // the out-of-date configuration as a fail-safe.  If it's not
                    // running, we're going to terminate the service.

                    if (!GetVarnishProcessIds().IsEmpty())
                    {
                        log.LogError(() => $"VARNISH-SHIM: Invalid Varnish configuration: {response.AllText}.");
                        log.LogError(() => $"VARNISH-SHIM: Using out-of-date configuration as a fail-safe.");
                    }
                    else
                    {
                        log.LogCritical(() => $"VARNISH-SHIM: Invalid Varnish configuration: {response.AllText}.");
                        log.LogCritical(() => "VARNISH-SHIM: Terminating service.");
                        Program.Exit(1);
                        return;
                    }
                }

                // Purge the contents of the [configFolder] and copy the contents
                // of [configUpdateFolder] into it.

                NeonHelper.DeleteFolder(configFolder);
                Directory.CreateDirectory(configFolder);
                NeonHelper.CopyFolder(configUpdateFolder, configFolder);

                // Start Varnish if it's not already running, otherwise command Varnish
                // to reload [varnish.vcl] as the main (and only) program.

                // $todo(jeff.lill): Should we do something different for DEBUG mode?

                if (!GetVarnishProcessIds().IsEmpty())
                {
                    log.LogInfo(() => $"VARNISH-SHIM: Updating Varnish.");

                    response = NeonHelper.ExecuteCapture("varnishadm",
                        new object[]
                        {
                            "vcl.load", VclProgram, configPath,
                            "-T", AdminInterface,
                        });

                    if (response.ExitCode == 0)
                    {
                        log.LogInfo(() => $"VARNISH-SHIM: Varnish has started.");
                    }
                    else
                    {
                        SetErrorTime();
                        log.LogError(() => $"VARNISH-SHIM: Cannot update Varnish: {response.ErrorText}");
                    }
                }
                else
                {
                    log.LogInfo(() => $"VARNISH-SHIM: Starting Vanish.");

                    response = NeonHelper.ExecuteCapture("varnishd",
                        new object[]
                        {
                            "-f", configPath,
                            "-s", $"malloc,{memoryLimit}",
                            "-T", AdminInterface,
                            "-a", "0.0.0.0:80",
                            "-n", workDir
                        });

                    if (response.ExitCode == 0)
                    {
                        log.LogInfo(() => $"VARNISH-SHIM: Varnish was updated.");
                    }
                    else
                    {
                        log.LogCritical(() => $"VARNISH-SHIM: Cannot start Varnish: {response.ErrorText}");
                        Program.Exit(1);
                        return;
                    }
                }

                // Update the deployed hash so we won't try to update the same 
                // configuration again.

                deployedHash = configHash;

                // Varnish was updated successfully so we can reset the error time
                // so to ensure that periodic error reporting will stop.

                ResetErrorTime();
            }
            catch (OperationCanceledException)
            {
                log.LogInfo(() => "VARNISH-SHIM: Terminating");
                throw;
            }
            finally
            {
                // When DEBUG mode is not enabled, we're going to clear the
                // both the old and new configuration folders so we don't leave
                // secrets like TLS private keys lying around in a file system.
                //
                // We'll leave these intact for DEBUG mode so we can manually
                // poke around the config.

                if (!debugMode)
                {
                    NeonHelper.DeleteFolder(configFolder);
                    NeonHelper.DeleteFolder(configUpdateFolder);
                }
            }
        }

        /// <summary>
        /// Sets the error time if it's not already set.  Errors will be 
        /// logged periodically when this is set.
        /// </summary>
        private static void SetErrorTime()
        {
            if (errorTimeUtc != DateTime.MinValue)
            {
                errorTimeUtc = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Clears the error time, disabling periodic error logging.
        /// </summary>
        private static void ResetErrorTime()
        {
            errorTimeUtc = DateTime.MinValue;
        }
    }
}