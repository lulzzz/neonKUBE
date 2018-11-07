﻿//-----------------------------------------------------------------------------
// FILE:	    HostPathMapping.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Hive;

namespace NeonProxyManager
{
    /// <summary>
    /// Describes a specific host/path to backend name for a load balancer rule.
    /// </summary>
    public class HostPathMapping
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="rule">The associated load balancer rule.</param>
        /// <param name="backendName">The backend name.</param>
        public HostPathMapping(LoadBalancerHttpRule rule, string backendName)
        {
            Covenant.Requires<ArgumentNullException>(rule != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(backendName));

            this.Rule        = rule;
            this.BackendName = backendName;
        }

        /// <summary>
        /// Returns thge associated load balancer rule.
        /// </summary>
        public LoadBalancerHttpRule Rule { get; private set; }

        /// <summary>
        /// Returns the backend name.
        /// </summary>
        public string BackendName { get; private set; }
    }
}
