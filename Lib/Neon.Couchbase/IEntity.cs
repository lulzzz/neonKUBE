﻿//-----------------------------------------------------------------------------
// FILE:	    Entity.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.ComponentModel;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;

namespace Neon.Data
{
    /// <summary>
    /// Interface describing an entity.  Most entity classes will will inherit a base
    /// implementation of this interface from <see cref="Entity{T}"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All entities must implement the <see cref="Type"/> property such that it returns
    /// the bucket unique string that identifies the entity type.  This string will be
    /// used to distinguish entity types within a Couchbase bucket.
    /// </para>
    /// <para>
    /// This interface supports the related concepts of entity <b>key</b> and <b>ref</b>.  The
    /// entity key is the string used to persist an entity instance to Couchbase.  By
    /// convention, this string is generally prefixed by the entity type and then is
    /// followed by instance specific properties, a UUID, or a singleton name.
    /// </para>
    /// <para>
    /// Entity <b>ref</b> is the value that other entities can use to reference an entity instance.
    /// This could be the same as the entity <b>key</b> but typically without the entity
    /// type prefix for brevity,
    /// </para>
    /// <para>
    /// Most entities should implement instance <see cref="GetKey()"/> to return the unique Couchbase
    /// key for the instance and entities that can be referenced by other entities should
    /// implement instance <see cref="GetRef()"/>.  Note that the base <see cref="Entity{T}"/> 
    /// implementation throws <see cref="NotSupportedException"/> for these methods.
    /// </para>
    /// <para>
    /// As a convention, many <see cref="IEntity{T}"/> implementations also implement <c>static</c>
    /// <b>GetKey(...)</b> and <b>GetRef(...)</b> methods that return the Couchbase key and
    /// reference to an entity based on parameters passed.
    /// </para>
    /// <para>
    /// Implement the <see cref="Equals(T)"/> method to compare one entity against another.
    /// The base <see cref="Entity{T}"/> implementation serializes both entities to JSON
    /// and compares them.
    /// </para>
    /// <para>
    /// Implement the <see cref="Normalize()"/> method to ensure that the entity properties
    /// are properly initialized.  The default <see cref="Entity{T}"/> implementation
    /// does nothing.
    /// </para>
    /// </remarks>
    public interface IEntity<T>
        where T : class, new()
    {
        /// <summary>
        /// Can be overridden by derived entities to return the Couchbase key to be used to
        /// persist the entity.  The base implementation throws a <see cref="NotSupportedException"/>.
        /// </summary>
        /// <returns>The Couchbase key for the entity.</returns>
        string GetKey();

        /// <summary>
        /// Can be overridden by derived entities to return the reference to be used to
        /// link to the entity instance.  The base implementation throws a <see cref="NotSupportedException"/>.
        /// </summary>
        /// <returns>The Couchbase ID for the entity.</returns>
        string GetRef();

        /// <summary>
        /// Identifies the entity type.
        /// </summary>
        string Type { get; set; }

        /// <summary>
        /// Tests this instance against another for equality.
        /// </summary>
        /// <param name="other">The other instance.</param>
        /// <returns><c>true</c> if the instances are equal.</returns>
        bool Equals(T other);

        /// <summary>
        /// Optionally ensures that the entity properties are properly
        /// initialized.  The default <see cref="Entity{T}"/> implementation
        /// does nothing.
        /// </summary>
        void Normalize();
    }
}
