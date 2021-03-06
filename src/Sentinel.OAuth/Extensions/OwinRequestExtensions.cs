﻿namespace Sentinel.OAuth.Extensions
{
    using System;

    using Microsoft.Owin;

    public static class OwinRequestExtensions
    {
        /// <summary>Creates an absolute url from the specified path.</summary>
        /// <param name="request">The request to act on.</param>
        /// <param name="path">The relative path.</param>
        /// <returns>The new absolute URL.</returns>
        public static Uri CreateAbsoluteUrl(this IOwinRequest request, string path)
        {
            if (string.IsNullOrEmpty(path) || !request.IsRelativeUrl(path))
            {
                throw new ArgumentException(nameof(path));
            }

            return new Uri($"{request.Uri.Scheme}://{request.Uri.Host}{path}");
        }

        /// <summary>Checks if the specified url is a relative url.</summary>
        /// <param name="request">The request to act on.</param>
        /// <param name="url">The url to check.</param>
        /// <returns>true if the url is relative, false if not.</returns>
        public static bool IsRelativeUrl(this IOwinRequest request, string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            // Check if it is a valid relative url
            if (url[0] == 47 && (url.Length == 1 || url[1] != 47 && url[1] != 92))
            {
                return true;
            }

            if (url.Length > 1 && url[0] == 126)
            {
                return url[1] == 47;
            }

            return false;
        }

        /// <summary>Compares the specified url with the request and determines if the url is on the same domain.</summary>
        /// <param name="request">The request to act on.</param>
        /// <param name="url">The url to compare.</param>
        /// <returns>true if url is on the same domain, false if not.</returns>
        public static bool IsLocalUrl(this IOwinRequest request, string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            // If it is an absolute url, validate that the base is the same as the current request
            Uri absoluteUri;
            if (Uri.TryCreate(url, UriKind.Absolute, out absoluteUri))
            {
                var host = new Uri(request.Uri.GetLeftPart(UriPartial.Authority));
                if (host.IsBaseOf(absoluteUri))
                {
                    return true;
                }

                return false;
            }

            // Check if it is a valid relative url
            return request.IsRelativeUrl(url);
        }

        /// <summary>Compares the specified url with the request and determines if it is the same.</summary>
        /// <param name="request">The request to act on.</param>
        /// <param name="url">The url to compare.</param>
        /// <returns>true if the url is the same, false if not.</returns>
        public static bool IsSameUrl(this IOwinRequest request, string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            // Validate absolute url
            Uri absoluteUri;
            if (Uri.TryCreate(url, UriKind.Absolute, out absoluteUri))
            {
                return request.Uri.ToString() == absoluteUri.ToString();
            }

            // Validate relative url
            if (request.Uri.PathAndQuery == url)
            {
                return true;
            }

            return false;
        }
    }
}