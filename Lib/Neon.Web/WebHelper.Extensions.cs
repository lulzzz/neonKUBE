﻿//-----------------------------------------------------------------------------
// FILE:	    WebHelper.Extensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;

namespace Neon.Web
{
    public static partial class WebHelper
    {
        //---------------------------------------------------------------------
        // Microsoft.AspNetCore.Http.HttpRequest extensions.

        /// <summary>
        /// Returns the full URI for an <see cref="HttpRequest"/> (not including the port number).
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The fully qualified URI including any query parameters.</returns>
        public static string GetUri(this HttpRequest request)
        {
            return $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}";
        }

        //---------------------------------------------------------------------
        // IMvcBuilder extensions

        /// <summary>
        /// Adds the services from an <see cref="IServiceContainer"/> to the <see cref="IServiceCollection"/>.
        /// This is commonly used when configuring services for an ASP.NET application pipeline.  This also
        /// calls <c>IMvcBuilder.AddNewtonsoftJson()</c> by default to enable JSON serialization using the
        /// same settings as returned by <see cref="NeonHelper.JsonRelaxedSerializerSettings"/>.
        /// </summary>
        /// <param name="builder">The target <see cref="IMvcBuilder"/>.</param>
        /// <param name="source">The service source container or <c>null</c> to copy from <see cref="NeonHelper.ServiceContainer"/>.</param>
        /// <param name="disableNewtonsoft">Optionally disable adding Newtonsoft JSON support.</param>
        public static IMvcBuilder AddNeon(IMvcBuilder builder, IServiceContainer source = null, bool disableNewtonsoft = false)
        {
            source = source ?? NeonHelper.ServiceContainer;

            foreach (var service in source)
            {
                builder.Services.Add(service);
            }

            if (!disableNewtonsoft)
            {
                builder.AddNewtonsoftJson(options => NeonHelper.JsonRelaxedSerializerSettings.Value.CopyTo(options.SerializerSettings));
            }

            return builder;
        }

        //---------------------------------------------------------------------
        // IMvcBuilder extensions

        /// <summary>
        /// Performs Neon related initialization including adding the the <see cref="RoundTripJsonInputFormatter"/>,
        /// <see cref="RoundTripJsonOutputFormatter"/> and <b>Newtonsoft JSON</b> formatters to the
        /// request pipeline.  These handle serialization and deserailzation of JSON text submitted to and
        /// returned from web services and classes generated by <b>Neon.CodeGen</b> implementing 
        /// <see cref="IGeneratedType"/> as well as many basic .NET types.
        /// </summary>
        /// <param name="builder">The MVC builder.</param>
        /// <param name="disableRoundTripFormatters">Optionally disable adding the round-trip formatters.</param>
        /// <param name="disableNewtonsoftFormatters">Optionally disable the Newtonsoft JSON formatters.</param>
        /// <remarks>
        /// <para>
        /// This provides both backwards and forwards data compatibility on both the client and service
        /// side by retaining object properties that one side or the other doesn't know about.  This enables
        /// scenarios where new properties are added to a data object but the solution components aren't
        /// all upgraded at the same time as a monolithic app.
        /// </para>
        /// </remarks>
        public static void AddNeon(this IMvcBuilder builder, bool disableRoundTripFormatters = false, bool disableNewtonsoftFormatters = false)
        {
            // Add any Newtonsodt formatters first so we can insert the round-trip
            // formatters before them below so the round-trip formatters will take
            // precedence.

            if (!disableNewtonsoftFormatters)
            {
                builder.AddNewtonsoftJson();
            }

            if (!disableRoundTripFormatters)
            {
                builder.AddMvcOptions(
                    options =>
                    {
                        options.InputFormatters.Insert(0, new RoundTripJsonInputFormatter());
                        options.OutputFormatters.Insert(0, new RoundTripJsonOutputFormatter());
                    });
            }
        }
    }
}
