﻿namespace Sentinel.OAuth.Implementation.Managers
{
    using System;

    using Sentinel.OAuth.Core.Constants.Identity;
    using Sentinel.OAuth.Core.Interfaces.Identity;
    using Sentinel.OAuth.Core.Interfaces.Providers;
    using Sentinel.OAuth.Core.Interfaces.Repositories;
    using Sentinel.OAuth.Core.Managers;
    using Sentinel.OAuth.Models.Identity;
    using System.Collections.Generic;
    using System.Security.Claims;
    using System.Threading.Tasks;

    using Common.Logging;

    using Microsoft.Owin.Security.OAuth;

    using Sentinel.OAuth.Core.Interfaces.Models;
    using Sentinel.OAuth.Core.Models;
    using Sentinel.OAuth.Core.Models.OAuth;

    public class ClientManager : BaseClientManager
    {
        /// <summary>Initializes a new instance of the <see cref="ClientManager" /> class.</summary>
        /// <param name="logger">The logger.</param>
        /// <param name="passwordCryptoProvider">The password crypto provider.</param>
        /// <param name="asymmetricCryptoProvider">The asymmetric crypto provider.</param>
        /// <param name="clientRepository">The client repository.</param>
        public ClientManager(ILog logger, IPasswordCryptoProvider passwordCryptoProvider, IAsymmetricCryptoProvider asymmetricCryptoProvider, IClientRepository clientRepository)
            : base(logger, passwordCryptoProvider, asymmetricCryptoProvider, clientRepository)
        {
        }

        /// <summary>Creates a client.</summary>
        /// <param name="clientId">The client id.</param>
        /// <param name="name">The name.</param>
        /// <param name="redirectUri">The redirect URI.</param>
        /// <returns>The new client.</returns>
        public override async Task<CreateClientResult> CreateClient(string clientId, string name, string redirectUri)
        {
            var client = new Client() { ClientId = clientId, Name = name, RedirectUri = redirectUri, Enabled = true };

            string clientSecret;
            string privateKey;

            client.ClientSecret = this.PasswordCryptoProvider.CreateHash(out clientSecret, 8);
            client.PublicKey = this.AsymmetricCryptoProvider.GenerateKeys(out privateKey);

            var result = await this.ClientRepository.Create(client);

            if (result != null)
            {
                return new CreateClientResult()
                           {
                               Client = result,
                               ClientSecret = clientSecret,
                               PrivateKey = privateKey
                           };
            }

            return null;
        }

        /// <summary>
        /// Authenticates the client. Used when authenticating with the authorization_code grant type.
        /// </summary>
        /// <param name="clientId">The client id.</param>
        /// <param name="redirectUri">The redirect URI.</param>
        /// <returns>The client principal.</returns>
        public override async Task<ISentinelPrincipal> AuthenticateClientAsync(string clientId, string redirectUri)
        {
            var client = await this.ClientRepository.GetClient(clientId);

            if (client != null && client.Enabled && client.RedirectUri == redirectUri)
            {
                var principal =
                    new SentinelPrincipal(
                        new SentinelIdentity(
                            AuthenticationType.OAuth,
                            new SentinelClaim(JwtClaimType.Name, clientId),
                            new SentinelClaim(ClaimTypes.NameIdentifier, client.ClientId),
                            new SentinelClaim(ClaimType.RedirectUri, client.RedirectUri),
                            new SentinelClaim(ClaimTypes.AuthenticationMethod, AuthenticationMethod.ClientId)));

                if (principal.Identity.IsAuthenticated)
                {
                    client.LastUsed = DateTimeOffset.UtcNow;
                    await this.ClientRepository.Update(client.GetIdentifier(), client);

                    return principal;
                }
            }

            return SentinelPrincipal.Anonymous;
        }

        /// <summary>
        /// Authenticates the client. Used when authenticating with the client_credentials grant type.
        /// </summary>
        /// <param name="clientId">The client id.</param>
        /// <param name="scope">The redirect URI.</param>
        /// <returns>The client principal.</returns>
        public override async Task<ISentinelPrincipal> AuthenticateClientAsync(string clientId, IEnumerable<string> scope)
        {
            var client = await this.ClientRepository.GetClient(clientId);

            if (client != null && client.Enabled)
            {
                var principal =
                        new SentinelPrincipal(
                            new SentinelIdentity(
                                AuthenticationType.OAuth,
                                new SentinelClaim(JwtClaimType.Name, clientId),
                                new SentinelClaim(ClaimTypes.NameIdentifier, client.ClientId),
                                new SentinelClaim(ClaimType.RedirectUri, client.RedirectUri),
                                new SentinelClaim(ClaimType.Scope, string.Join(" ", scope)),
                                new SentinelClaim(ClaimTypes.AuthenticationMethod, AuthenticationMethod.ClientId)));

                if (principal.Identity.IsAuthenticated)
                {
                    client.LastUsed = DateTimeOffset.UtcNow;
                    await this.ClientRepository.Update(client.GetIdentifier(), client);

                    return principal;
                }
            }

            return SentinelPrincipal.Anonymous;
        }

        public override async Task<ISentinelPrincipal> AuthenticateClientCredentialsAsync(BasicAuthenticationDigest digest)
        {
            BasicAuthenticationCipher cipher;

            try
            {
                cipher = digest.GetCipher();
            }
            catch (ArgumentException)
            {
                return SentinelPrincipal.Anonymous;
            }

            var client = await this.ClientRepository.GetClient(digest.UserId);
            if (client != null && client.Enabled && client.RedirectUri == cipher.RedirectUri)
            {
                if (this.PasswordCryptoProvider.ValidateHash(cipher.Password, client.ClientSecret))
                {
                    var principal =
                        new SentinelPrincipal(
                            new SentinelIdentity(
                                AuthenticationType.Basic,
                                new SentinelClaim(JwtClaimType.Name, client.ClientId),
                                new SentinelClaim(ClaimTypes.NameIdentifier, client.ClientId),
                                new SentinelClaim(ClaimType.Client, client.ClientId),
                                new SentinelClaim(ClaimType.RedirectUri, client.RedirectUri),
                                new SentinelClaim(ClaimType.AuthenticationSource, "local"),
                                new SentinelClaim(ClaimTypes.AuthenticationMethod, AuthenticationMethod.Basic)));

                    if (principal.Identity.IsAuthenticated)
                    {
                        client.LastUsed = DateTimeOffset.UtcNow;
                        await this.ClientRepository.Update(client.GetIdentifier(), client);

                        return principal;
                    }
                }
            }

            return SentinelPrincipal.Anonymous;
        }

        /// <summary>Authenticates the client using client id and secret.</summary>
        /// <param name="clientId">The client id.</param>
        /// <param name="clientSecret">The client secret.</param>
        /// <returns>The client principal.</returns>
        public override async Task<ISentinelPrincipal> AuthenticateClientCredentialsAsync(string clientId, string clientSecret)
        {
            var client = await this.ClientRepository.GetClient(clientId);

            if (client != null && client.Enabled)
            {
                if (this.PasswordCryptoProvider.ValidateHash(clientSecret, client.ClientSecret))
                {
                    var principal =
                        new SentinelPrincipal(
                            new SentinelIdentity(
                                AuthenticationType.OAuth,
                                new SentinelClaim(JwtClaimType.Name, client.ClientId),
                                new SentinelClaim(ClaimTypes.NameIdentifier, client.ClientId),
                                new SentinelClaim(ClaimType.Client, client.ClientId),
                                new SentinelClaim(ClaimType.RedirectUri, client.RedirectUri),
                                new SentinelClaim(ClaimTypes.AuthenticationMethod, AuthenticationMethod.ClientCredentials)));

                    if (principal.Identity.IsAuthenticated)
                    {
                        client.LastUsed = DateTimeOffset.UtcNow;
                        await this.ClientRepository.Update(client.GetIdentifier(), client);

                        return principal;
                    }
                }
            }

            return SentinelPrincipal.Anonymous;
        }

        /// <summary>Authenticate client using API Key.</summary>
        /// <exception cref="ArgumentException">
        /// Thrown when one or more arguments have unsupported or illegal values.
        /// </exception>
        /// <param name="digest">The digest.</param>
        /// <returns>The client principal.</returns>
        public override async Task<ISentinelPrincipal> AuthenticateClientWithSignatureAsync(SignatureAuthenticationDigest digest)
        {
            // 1. Validate client id and redirect uri
            var client = await this.ClientRepository.GetClient(digest.ClientId);

            if (client == null || !client.Enabled)
            {
                throw new ArgumentException(nameof(digest), "The client_id is invalid");
            }

            if (client.RedirectUri != digest.RedirectUri)
            {
                throw new ArgumentException(nameof(digest), "The redirect_uri is invalid");
            }

            // 2. Validate username and signature using client secret
            if (digest.UserId != client.ClientId)
            {
                throw new ArgumentException(nameof(digest), "The user_id is invalid, must be equal to client_id");
            }

            if (!string.IsNullOrEmpty(client.PublicKey))
            {
                var isValid = this.AsymmetricCryptoProvider.ValidateSignature(digest.GetData(), digest.Signature, client.PublicKey);
                if (isValid)
                {
                    var principal =
                        new SentinelPrincipal(
                            new SentinelIdentity(
                                AuthenticationType.Signature,
                                new SentinelClaim(JwtClaimType.Name, client.ClientId),
                                new SentinelClaim(ClaimTypes.NameIdentifier, client.ClientId),
                                new SentinelClaim(ClaimType.Client, client.ClientId),
                                new SentinelClaim(ClaimType.RedirectUri, client.RedirectUri),
                                new SentinelClaim(ClaimTypes.AuthenticationMethod, AuthenticationMethod.Signature)));

                    if (principal.Identity.IsAuthenticated)
                    {
                        client.LastUsed = DateTimeOffset.UtcNow;
                        await this.ClientRepository.Update(client.GetIdentifier(), client);

                        return principal;
                    }

                    return principal;
                }
            }

            return SentinelPrincipal.Anonymous;
        }
    }
}