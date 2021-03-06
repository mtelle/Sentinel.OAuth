﻿namespace Sentinel.OAuth.Middleware
{
    using System;

    using Common.Logging;

    using Microsoft.Owin.Security;

    using Sentinel.OAuth.Core.Interfaces.Managers;

    public class SignatureAuthenticationOptions : AuthenticationOptions
    {
        /// <summary>Initializes a new instance of the <see cref="SignatureAuthenticationOptions" /> class.</summary>
        public SignatureAuthenticationOptions()
            : base("Signature")
        {
        }

        /// <summary>Gets or sets the realm.</summary>
        /// <value>The realm.</value>
        public string Realm { get; set; }

        /// <summary>Gets or sets the logger.</summary>
        /// <value>The logger.</value>
        public ILog Logger { get; set; }

        /// <summary>Gets or sets the maximum clock skew.</summary>
        /// <value>The maximum clock skew.</value>
        public TimeSpan MaximumClockSkew { get; set; }

        /// <summary>Gets or sets a value indicating whether to require a secure connection.</summary>
        /// <value>true if a secure connection is required, false if not.</value>
        public bool RequireSecureConnection { get; set; }

        /// <summary>
        /// Gets or sets the user management provider. This is the class responsible for locating and
        /// validating users.
        /// </summary>
        /// <value>The user management provider.</value>
        public IUserManager UserManager { get; set; }

        /// <summary>
        /// Gets or sets the client management provider. This is the class responsible for locating and
        /// validating clients.
        /// </summary>
        /// <value>The client management provider.</value>
        public IClientManager ClientManager { get; set; }
    }
}