﻿//-----------------------------------------------------------------------------
// FILE:	    DockerOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Hive
{
    /// <summary>
    /// Describes the Docker options for a neonHIVE.
    /// </summary>
    public class DockerOptions
    {
        private const string    defaultLogOptions         = "--log-driver=fluentd --log-opt tag= --log-opt fluentd-async-connect=true";
        private const bool      defaultRegistryCache      = true;
        private const string    defaultRegistryCacheImage = HiveConst.NeonPublicRegistry + "/neon-registry-cache:latest";
        private const bool      defaultExperimental       = false;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public DockerOptions()
        {
        }

        /// <summary>
        /// Returns the Swarm node advertise port.
        /// </summary>
        [JsonIgnore]
        public int SwarmPort
        {
            get { return NetworkPorts.DockerSwarm; }
        }

        /// <summary>
        /// <para>
        /// The version of Docker to be installed like [17.03.0-ce].  You may also specify <b>latest</b>
        /// to install the most recent production release or <b>test</b> or <b>experimental</b> to
        /// install the latest releases from the test or experimental channels.
        /// </para>
        /// <note>
        /// Only Community Editions of Docker are supported at this time.
        /// </note>
        /// <para>
        /// This defaults to <b>latest</b>.
        /// </para>
        /// <note>
        /// <para><b>IMPORTANT!</b></para>
        /// <para>
        /// Production hives should always install a specific version of Docker so 
        /// it will be easy to add new hosts in the future that will have the same 
        /// Docker version as the rest of the hive.  This also prevents the package
        /// manager from inadvertently upgrading Docker.
        /// </para>
        /// </note>
        /// <note>
        /// <para><b>IMPORTANT!</b></para>
        /// <para>
        /// It is not possible for the <b>neon-cli</b> tool to upgrade Docker on hives
        /// that deployed the <b>test</b> or <b>experimental</b> build.
        /// </para>
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Version", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("latest")]
        public string Version { get; set; } = "latest";

        /// <summary>
        /// Returns <b>latest</b>, <b>test</b>, or <b>experimental</b> for current releases 
        /// or the specific Ubuntu APT package version to be installed.
        /// </summary>
        [JsonIgnore]
        public string PackageVersion
        {
            get
            {
                var version = Version.ToLowerInvariant();

                switch (version)
                {
                    case "latest":
                    case "test":
                    case "experimental":

                        return version;

                    default:

                        // Remove the "-ce" from the end of the version and then
                        // return fully qualified package version.

                        if (!version.EndsWith("-ce"))
                        {
                            throw new NotSupportedException("Docker version must end with [-ce] because only Docker Community Edition is supported at this time.");
                        }

                        // $todo(jeff.lill):
                        //
                        // This is a bit of a mess.  It appears that releases before [17.06.0-ce]
                        // append "~ubuntu-xenial" to the version and versions after that just append
                        // "~ubuntu".  I really believe I'm going to need to maintain a global 
                        // registry of Docker versions so I don't have to keep messing with the
                        // [neon-cli].

                        version = version.Substring(0, version.Length - "-ce".Length);

                        if (new Version(version) >= new System.Version("17.06.0"))
                        {
                            return $"{version}~ce-0~ubuntu";
                        }
                        else
                        {
                            return $"{version}~ce-0~ubuntu-xenial";
                        }
                }
            }
        }

        /// <summary>
        /// Specifies the Docker Registries and the required credentials that will
        /// be made available to the hive.  Note that the Docker public registry
        /// will always be available to new hives.
        /// </summary>
        [JsonProperty(PropertyName = "Registries", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<RegistryCredentials> Registries { get; set; } = new List<RegistryCredentials>();

        /// <summary>
        /// Optionally indicates that local pull-thru Docker registry caches are to be deployed
        /// on the hive manager nodes.  This defaults to <c>true</c>.
        /// </summary>
        [JsonProperty(PropertyName = "RegistryCache", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultRegistryCache)]
        public bool RegistryCache { get; set; } = defaultRegistryCache;

        /// <summary>
        /// Optionally specifies the Docker image to be used to deploy the registry cache.
        /// This defaults to <b>nhive/neon-registry-cache:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "RegistryCacheImage", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultRegistryCacheImage)]
        public string RegistryCacheImage { get; set; } = defaultRegistryCacheImage;

        /// <summary>
        /// <para>
        /// The Docker daemon container logging options.  This defaults to:
        /// </para>
        /// <code language="none">
        /// --log-driver=fluentd --log-opt tag= --log-opt fluentd-async-connect=true
        /// </code>
        /// <para>
        /// which by default, will forward container logs to the hive logging pipeline.
        /// </para>
        /// </summary>
        /// <remarks>
        /// <note>
        /// <b>IMPORTANT:</b> Always use the <b>--log-opt fluentd-async-connect=true</b> option
        /// when using the <b>fluentd</b> log driver.  Containers without this will stop if
        /// the logging pipeline is not ready when the container starts.
        /// </note>
        /// <para>
        /// You may have individual services and containers opt out of hive logging by setting
        /// <b>--log-driver=json-text</b> or <b>--log-driver=none</b>.  This can be handy while
        /// debugging Docker images.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "LogOptions", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultLogOptions)]
        public string LogOptions { get; set; } = defaultLogOptions;

        /// <summary>
        /// The seconds Docker should wait before restarting a container or service instance.
        /// This defaults to 10 seconds.
        /// </summary>
        [JsonProperty(PropertyName = "RestartDelaySeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(10)]
        public int RestartDelaySeconds { get; set; } = 10;

        /// <summary>
        /// Returns the <see cref="RestartDelaySeconds"/> property converted to a string with
        /// a seconds unit appended, suitable for passing to a Docker command.
        /// </summary>
        [JsonIgnore]
        public string RestartDelay
        {
            get { return $"{RestartDelaySeconds}s"; }
        }

        /// <summary>
        /// Controls whether the Docker Ingress network is used for for hive proxies.  This defaults to <c>null</c>
        /// which is currently equivalent to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "AvoidIngressNetwork", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public bool? AvoidIngressNetwork { get; set; } = null;

        /// <summary>
        /// <b>Internal Use Only:</b> Indicates whether the Docker ingress network should be used
        /// for load balancer instances based on <see cref="AvoidIngressNetwork"/> and the the
        /// current hosting environment.
        /// </summary>
        /// <param name="hiveDefinition">The current hive definition.</param>
        public bool GetAvoidIngressNetwork(HiveDefinition hiveDefinition)
        {
            Covenant.Requires<ArgumentNullException>(hiveDefinition != null);

            if (AvoidIngressNetwork.HasValue)
            {
                return AvoidIngressNetwork.Value;
            }

            // $todo(jeff.lill):
            //
            // We were having problems with the ingress network in the past so the
            // commented out code below used to avoid the ingress network when
            // deploying on local Hyper-V.  We'll leave this commented out for the
            // time being but if the problem doesn't resurface, we should delete it.
            //
            //      https://github.com/jefflill/NeonForge/issues/104

            //return hiveDefinition.Hosting.Environment == HostingEnvironments.HyperVDev;

            return false;
        }

        /// <summary>
        /// Enables experimental Docker features.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Experimental", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultExperimental)]
        public bool Experimental { get; set; } = defaultExperimental;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <exception cref="HiveDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(HiveDefinition hiveDefinition)
        {
            Covenant.Requires<ArgumentNullException>(hiveDefinition != null);

            Version             = Version ?? "latest";
            Registries = Registries ?? new List<RegistryCredentials>();

            if (RestartDelaySeconds < 0)
            {
                RestartDelaySeconds = 0;
            }

            var version = Version.Trim().ToLower();
            Uri uri     = null;

            if (version == "latest" ||
                version == "test" ||
                version == "experimental")
            {
                Version = version.ToLowerInvariant();
            }
            else
            {
                Version = version.ToLowerInvariant();
                uri     = new Uri($"https://codeload.github.com/moby/moby/tar.gz/v{version}");

                if (!version.EndsWith("-ce"))
                {
                    throw new HiveDefinitionException($"[{nameof(DockerOptions)}.{Version}] does not specify a Docker community edition.  neonHIVE only supports Docker Community Edition at this time.");
                }
            }

            // $todo(jeff.lill): 
            //
            // This check doesn't work for non-stable releases now that Docker has implemented
            // the new stable, edge, testing release channel scheme.  At some point, it would
            // be interesting to try to figure out another way.
            //
            // Probably the best approach would be to actually use [apt-get] to list the 
            // available versions.  This would look something like:
            //
            //      # Configure the stable, edge, and testing repositorties
            //  
            //      add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable"
            //      add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/ubuntu $(lsb_release -cs) edge"
            //      add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/ubuntu $(lsb_release -cs) testing"
            //      apt-get update
            //
            // and then use the following the list the versions:
            //
            //      apt-get install -yq docker-ce=${docker_version}
            //
            // I'm doubtful that it's possible to implement this directly in the [neon-cli].
            // One approach would be to have a service that polls [apt-get] for this a few
            // times a day and then exposes a REST API that can answer the question.
#if TODO
            if (uri != null)
            {
                // Verify that the Docker download actually exists.

                using (var client = new HttpClient())
                {
                    try
                    {
                        var request  = new HttpRequestMessage(HttpMethod.Head, uri);
                        var response = client.SendAsync(request).Result;

                        response.EnsureSuccessStatusCode();
                    }
                    catch (Exception e)
                    {
                        throw new HiveDefinitionException($"Cannot confirm that Docker release [{version}] exists at [{uri}].  {NeonHelper.ExceptionError(e)}");
                    }
                }
            }
#endif
            foreach (var registry in Registries)
            {
                var hostname = registry.Registry;

                if (string.IsNullOrEmpty(hostname) || !HiveDefinition.DnsHostRegex.IsMatch(hostname))
                {
                    throw new HiveDefinitionException($"[{nameof(DockerOptions)}.{nameof(Registries)}] includes a [{nameof(Neon.Hive.RegistryCredentials.Registry)}={hostname}] is not a valid registry hostname.");
                }
            }
        }

        /// <summary>
        /// Clears any sensitive properties like the Docker registry credentials.
        /// </summary>
        public void ClearSecrets()
        {
            Registries.Clear();
        }
    }
}