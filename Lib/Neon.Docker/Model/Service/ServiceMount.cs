﻿//-----------------------------------------------------------------------------
// FILE:	    ServiceMount.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Neon.Docker
{
    /// <summary>
    /// Service mount specification.
    /// </summary>
    public class ServiceMount : INormalizable
    {
        /// <summary>
        /// Specifies where the mount will appear within the service containers. 
        /// </summary>
        [JsonProperty(PropertyName = "Target", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string Target { get; set; }

        /// <summary>
        /// Specifies the external mount source
        /// </summary>
        [JsonProperty(PropertyName = "Source", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string Source { get; set; }

        /// <summary>
        /// The mount type.
        /// </summary>
        [JsonProperty(PropertyName = "Type", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(default(ServiceMountType))]
        public ServiceMountType Type { get; set; }

        /// <summary>
        /// Specifies whether the mount is to be read-only within the service containers.
        /// </summary>
        [JsonProperty(PropertyName = "ReadOnly", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(false)]
        public bool ReadOnly { get; set; }

        /// <summary>
        /// Specifies the mount consistency.
        /// </summary>
        [JsonProperty(PropertyName = "Consistency", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(default(ServiceMountConsistency))]
        public ServiceMountConsistency Consistency { get; set; }

        /// <summary>
        /// Specifies the bind propagation mode.
        /// </summary>
        [JsonProperty(PropertyName = "BindOptions", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public ServiceBindOptions BindOptions { get; set; }

        /// <summary>
        /// Specifies volume mount configuration options.
        /// </summary>
        [JsonProperty(PropertyName = "VolumeOptions", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public ServiceVolumeOptions VolumeOptions { get; set; }

        /// <summary>
        /// Specifies Tempfs mount configuration options.
        /// </summary>
        [JsonProperty(PropertyName = "TempfsOptions", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public ServiceTmpfsOptions TempfsOptions { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
