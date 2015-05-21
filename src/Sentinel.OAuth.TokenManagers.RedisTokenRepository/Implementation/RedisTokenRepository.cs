﻿namespace Sentinel.OAuth.TokenManagers.RedisTokenRepository.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Newtonsoft.Json;

    using Sentinel.OAuth.Core.Interfaces.Models;
    using Sentinel.OAuth.Core.Interfaces.Repositories;
    using Sentinel.OAuth.TokenManagers.RedisTokenRepository.Extensions;
    using Sentinel.OAuth.TokenManagers.RedisTokenRepository.Models;

    using StackExchange.Redis;

    /// <summary>A token repository using Redis for storage.</summary>
    public class RedisTokenRepository : ITokenRepository
    {
        /// <summary>The date time maximum.</summary>
        private const double DateTimeMax = 253402300800.0;

        /// <summary>The configuration.</summary>
        private readonly RedisTokenRepositoryConfiguration configuration;

        /// <summary>
        /// Initializes a new instance of the RedisTokenRepository class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public RedisTokenRepository(RedisTokenRepositoryConfiguration configuration)
        {
            this.configuration = configuration;
        }

        /// <summary>
        /// Gets all authorization codes that matches the specified redirect uri and expires after the
        /// specified date. Called when authenticating an authorization code.
        /// </summary>
        /// <param name="redirectUri">The redirect uri.</param>
        /// <param name="expires">The expire date.</param>
        /// <returns>The authorization codes.</returns>
        public async Task<IEnumerable<IAuthorizationCode>> GetAuthorizationCodes(string redirectUri, DateTime expires)
        {
            var db = this.GetDatabase();

            var min = expires.ToUnixTime();
            var codes = new List<IAuthorizationCode>();

            var keys = db.SortedSetRangeByScore(this.configuration.AuthorizationCodePrefix, min, DateTimeMax);

            foreach (var key in keys)
            {
                var hashedId = key.ToString().Substring(this.configuration.AuthorizationCodePrefix.Length + 1);
                var id = Encoding.UTF8.GetString(Convert.FromBase64String(hashedId));

                if (id.Contains(redirectUri))
                {
                    var hashEntries = await db.HashGetAllAsync(key.ToString());

                    if (hashEntries.Any())
                    {
                        var code = new RedisAuthorizationCode(hashEntries) { Id = hashedId };

                        codes.Add(code);
                    }
                }
            }

            return codes;
        }

        /// <summary>
        /// Inserts the specified authorization code. Called when creating an authorization code.
        /// </summary>
        /// <param name="authorizationCode">The authorization code.</param>
        /// <returns>
        /// The inserted authorization code. <c>null</c> if the insertion was unsuccessful.
        /// </returns>
        public async Task<IAuthorizationCode> InsertAuthorizationCode(IAuthorizationCode authorizationCode)
        {
            var code = (RedisAuthorizationCode)authorizationCode;

            var key = this.GenerateKey(code);

            var db = this.GetDatabase();

            try
            {
                if (code.ClientId == null || code.RedirectUri == null || code.Subject == null
                    || code.Code == null || code.Ticket == null
                    || code.Created == DateTime.MinValue
                    || code.ValidTo == DateTime.MinValue)
                {
                    throw new ArgumentException(string.Format("The authorization code is invalid: {0}", JsonConvert.SerializeObject(code)), "authorizationCode");
                }

                this.configuration.Log.DebugFormat("Inserting access token hash in key {0}", key);

                // Add hash to key
                await db.HashSetAsync(key, code.ToHashEntries());

                var expires = authorizationCode.ValidTo.ToUnixTime();

                this.configuration.Log.DebugFormat("Inserting key {0} to authorization code set with score {1}", key, expires);

                // Add key to sorted set for future reference. The score is the expire time in seconds since epoch.
                await db.SortedSetAddAsync(this.configuration.AuthorizationCodePrefix, key, expires);

                this.configuration.Log.DebugFormat("Making key {0} expire at {1}", key, authorizationCode.ValidTo);

                // Make the key expire when the code times out
                await db.KeyExpireAsync(key, authorizationCode.ValidTo);

                return authorizationCode;
            }
            catch (Exception ex)
            {
                this.configuration.Log.Error("Error when inserting authorization code", ex);
            }

            return null;
        }

        /// <summary>
        /// Deletes the authorization codes that expires before the specified expire date. Called when
        /// creating an authorization code to cleanup.
        /// </summary>
        /// <param name="expires">The expire date.</param>
        /// <returns>The number of deleted codes.</returns>
        public async Task<int> DeleteAuthorizationCodes(DateTime expires)
        {
            var db = this.GetDatabase();

            // Remove items from set
            // We don't need to remove the keys themselves, as Redis will remove them for us because we set the EXPIRE parameter.
            var i = await db.SortedSetRemoveRangeByScoreAsync(this.configuration.AuthorizationCodePrefix, 0, expires.ToUnixTime());

            return (int)i;
        }

        /// <summary>
        /// Deletes the specified authorization code. Called when authenticating an authorization code to
        /// prevent re-use.
        /// </summary>
        /// <param name="authorizationCode">The authorization code.</param>
        /// <returns><c>True</c> if successful, <c>false</c> otherwise.</returns>
        public async Task<bool> DeleteAuthorizationCode(IAuthorizationCode authorizationCode)
        {
            var code = (RedisAuthorizationCode)authorizationCode;

            var key = this.GenerateKey(code);

            var db = this.GetDatabase();

            // Remove items from set
            // We don't need to remove the keys themselves, as Redis will remove them for us because we set the EXPIRE parameter.
            return await db.SortedSetRemoveAsync(this.configuration.AuthorizationCodePrefix, key);
        }

        /// <summary>
        /// Gets all access tokens that expires **after** the specified date. Called when authenticating
        /// an access token to limit the number of tokens to go through when validating the hash.
        /// </summary>
        /// <param name="expires">The expire date.</param>
        /// <returns>The access tokens.</returns>
        public async Task<IEnumerable<IAccessToken>> GetAccessTokens(DateTime expires)
        {
            var db = this.GetDatabase();

            var min = expires.ToUnixTime();
            var tokens = new List<IAccessToken>();

            var keys = db.SortedSetRangeByScore(this.configuration.AccessTokenPrefix, min, DateTimeMax);

            foreach (var key in keys)
            {
                var hashedId = key.ToString().Substring(this.configuration.AccessTokenPrefix.Length + 1);

                var hashEntries = await db.HashGetAllAsync(key.ToString());

                if (hashEntries.Any())
                {
                    var token = new RedisAccessToken(hashEntries) { Id = hashedId };

                    tokens.Add(token);
                }
            }

            return tokens;
        }

        /// <summary>Inserts the specified access token. Called when creating an access token.</summary>
        /// <param name="accessToken">The access token.</param>
        /// <returns>The inserted access token. <c>null</c> if the insertion was unsuccessful.</returns>
        public async Task<IAccessToken> InsertAccessToken(IAccessToken accessToken)
        {
            var token = (RedisAccessToken)accessToken;

            var key = this.GenerateKey(token);

            var db = this.GetDatabase();

            try
            {
                if (token.ClientId == null || token.RedirectUri == null || token.Subject == null
                    || token.Token == null || token.Ticket == null
                    || token.Created == DateTime.MinValue
                    || token.ValidTo == DateTime.MinValue)
                {
                    throw new ArgumentException(string.Format("The access token is invalid: {0}", JsonConvert.SerializeObject(token)), "accessToken");
                }

                this.configuration.Log.DebugFormat("Inserting access token hash in key {0}", key);

                // Add hash to key
                await db.HashSetAsync(key, token.ToHashEntries());

                var expires = accessToken.ValidTo.ToUnixTime();

                this.configuration.Log.DebugFormat("Inserting key {0} to access token set with score {1}", key, expires);

                // Add key to sorted set for future reference. The score is the expire time in seconds since epoch.
                await db.SortedSetAddAsync(this.configuration.AccessTokenPrefix, key, expires);

                this.configuration.Log.DebugFormat("Making key {0} expire at {1}", key, accessToken.ValidTo);

                // Make the key expire when the code times out
                await db.KeyExpireAsync(key, accessToken.ValidTo);

                return accessToken;
            }
            catch (Exception ex)
            {
                this.configuration.Log.Error("Error when inserting access token", ex);
            }

            return null;
        }

        /// <summary>
        /// Deletes the access tokens that expires before the specified expire date. Called when creating
        /// an access token to cleanup.
        /// </summary>
        /// <param name="expires">The expire date.</param>
        /// <returns>The number of deleted tokens.</returns>
        public async Task<int> DeleteAccessTokens(DateTime expires)
        {
            var db = this.GetDatabase();

            // Remove items from set
            // We don't need to remove the keys themselves, as Redis will remove them for us because we set the EXPIRE parameter.
            var i = await db.SortedSetRemoveRangeByScoreAsync(this.configuration.AccessTokenPrefix, 0, expires.ToUnixTime());

            return (int)i;
        }

        /// <summary>Deletes the specified access token.</summary>
        /// <param name="accessToken">The access token.</param>
        /// <returns><c>True</c> if successful, <c>false</c> otherwise.</returns>
        public async Task<bool> DeleteAccessToken(IAccessToken accessToken)
        {
            var token = (RedisAccessToken)accessToken;

            var key = this.GenerateKey(token);

            var db = this.GetDatabase();

            // Remove items from set
            // We don't need to remove the keys themselves, as Redis will remove them for us because we set the EXPIRE parameter.
            return await db.SortedSetRemoveAsync(this.configuration.AccessTokenPrefix, key);
        }

        /// <summary>
        /// Gets all refresh tokens that matches the specified redirect uri and expires after the
        /// specified date. Called when authentication a refresh token to limit the number of tokens to
        /// go through when validating the hash.
        /// </summary>
        /// <param name="redirectUri">The redirect uri.</param>
        /// <param name="expires">The expire date.</param>
        /// <returns>The refresh tokens.</returns>
        public async Task<IEnumerable<IRefreshToken>> GetRefreshTokens(string redirectUri, DateTime expires)
        {
            var db = this.GetDatabase();

            var min = expires.ToUnixTime();
            var tokens = new List<IRefreshToken>();

            var keys = db.SortedSetRangeByScore(this.configuration.RefreshTokenPrefix, min, DateTimeMax);

            foreach (var key in keys)
            {
                var hashedId = key.ToString().Substring(this.configuration.AccessTokenPrefix.Length + 1);

                var hashEntries = await db.HashGetAllAsync(key.ToString());

                if (hashEntries.Any())
                {
                    var token = new RedisRefreshToken(hashEntries) { Id = hashedId };

                    tokens.Add(token);
                }
            }

            return tokens;
        }

        /// <summary>Inserts the specified refresh token. Called when creating a refresh token.</summary>
        /// <param name="refreshToken">The refresh token.</param>
        /// <returns>The inserted refresh token. <c>null</c> if the insertion was unsuccessful.</returns>
        public async Task<IRefreshToken> InsertRefreshToken(IRefreshToken refreshToken)
        {
            var token = (RedisRefreshToken)refreshToken;

            var key = this.GenerateKey(token);

            var db = this.GetDatabase();

            try
            {
                if (token.ClientId == null || token.RedirectUri == null || token.Subject == null
                    || token.Token == null
                    || token.Created == DateTime.MinValue
                    || token.ValidTo == DateTime.MinValue)
                {
                    throw new ArgumentException(string.Format("The refresh token is invalid: {0}", JsonConvert.SerializeObject(token)), "refreshToken");
                }

                this.configuration.Log.DebugFormat("Inserting refresh token hash in key {0}", key);

                // Add hash to key
                await db.HashSetAsync(key, token.ToHashEntries());

                var expires = refreshToken.ValidTo.ToUnixTime();

                this.configuration.Log.DebugFormat("Inserting key {0} to refresh token set with score {1}", key, expires);

                // Add key to sorted set for future reference. The score is the expire time in seconds since epoch.
                await db.SortedSetAddAsync(this.configuration.RefreshTokenPrefix, key, expires);

                this.configuration.Log.DebugFormat("Making key {0} expire at {1}", key, refreshToken.ValidTo);

                // Make the key expire when the code times out
                await db.KeyExpireAsync(key, refreshToken.ValidTo);

                return refreshToken;
            }
            catch (Exception ex)
            {
                this.configuration.Log.Error("Error when inserting refresh token", ex);
            }

            return null;
        }

        /// <summary>
        /// Deletes the refresh tokens that expires before the specified expire date. Called when
        /// creating a refresh token to cleanup.
        /// </summary>
        /// <param name="expires">The expire date.</param>
        /// <returns>The number of deleted tokens.</returns>
        public async Task<int> DeleteRefreshTokens(DateTime expires)
        {
            var db = this.GetDatabase();

            // Remove items from set
            // We don't need to remove the keys themselves, as Redis will remove them for us because we set the EXPIRE parameter.
            var i = await db.SortedSetRemoveRangeByScoreAsync(this.configuration.RefreshTokenPrefix, 0, expires.ToUnixTime());

            return (int)i;
        }

        /// <summary>
        /// Deletes the specified refresh token. Called when authenticating a refresh token to prevent re-
        /// use.
        /// </summary>
        /// <param name="refreshToken">The refresh token.</param>
        /// <returns><c>True</c> if successful, <c>false</c> otherwise.</returns>
        public async Task<bool> DeleteRefreshToken(IRefreshToken refreshToken)
        {
            var token = (RedisRefreshToken)refreshToken;

            var key = this.GenerateKey(token);

            var db = this.GetDatabase();

            // Remove items from set
            // We don't need to remove the keys themselves, as Redis will remove them for us because we set the EXPIRE parameter.
            return await db.SortedSetRemoveAsync(this.configuration.RefreshTokenPrefix, key);
        }

        /// <summary>Generates a key.</summary>
        /// <param name="accessToken">The access token.</param>
        /// <returns>The key.</returns>
        private string GenerateKey(RedisAccessToken accessToken)
        {
            return this.configuration.AccessTokenPrefix + ":" + accessToken.Id;
        }

        /// <summary>Generates a key.</summary>
        /// <param name="refreshToken">The refresh token.</param>
        /// <returns>The key.</returns>
        private string GenerateKey(RedisRefreshToken refreshToken)
        {
            return this.configuration.RefreshTokenPrefix + ":" + refreshToken.Id;
        }

        /// <summary>Generates a key.</summary>
        /// <param name="authorizationCode">The authorization code.</param>
        /// <returns>The key.</returns>
        private string GenerateKey(RedisAuthorizationCode authorizationCode)
        {
            return this.configuration.AuthorizationCodePrefix + ":" + authorizationCode.Id;
        }

        /// <summary>Gets a reference to the database.</summary>
        /// <returns>A reference to database.</returns>
        private IDatabase GetDatabase()
        {
            return this.configuration.Connection.GetDatabase(this.configuration.Database);
        }
    }
}