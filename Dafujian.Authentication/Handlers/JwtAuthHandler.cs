﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using System.Web.Script.Serialization;
using JWT;
using Microsoft.Owin;

namespace Dafujian.Authentication.Handlers
{
    public class JwtAuthHandler : DelegatingHandler
    {
        //protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        //            CancellationToken cancellationToken)
        //{
        //    HttpResponseMessage errorResponse = null;

        //    try
        //    {
        //        IEnumerable<string> authHeaderValues;
        //        request.Headers.TryGetValues("Authorization", out authHeaderValues);


        //        if (authHeaderValues == null)
        //            return base.SendAsync(request, cancellationToken); // cross fingers

        //        var bearerToken = authHeaderValues.ElementAt(0);
        //        var token = bearerToken.StartsWith("Bearer ") ? bearerToken.Substring(7) : bearerToken;
        //        var secret = WebConfigurationManager.AppSettings.Get("jwtKey");
        //        Thread.CurrentPrincipal = ValidateToken(
        //            token,
        //            secret,
        //            true
        //            );

        //        if (HttpContext.Current != null)
        //        {
        //            HttpContext.Current.User = Thread.CurrentPrincipal;
        //        }
        //    }
        //    catch (SignatureVerificationException ex)
        //    {
        //        errorResponse = request.CreateErrorResponse(HttpStatusCode.Unauthorized, ex.Message);
        //    }
        //    catch (Exception ex)
        //    {
        //        errorResponse = request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
        //    }


        //    return errorResponse != null
        //        ? Task.FromResult(errorResponse)
        //        : base.SendAsync(request, cancellationToken);
        //}

        public static void OnAuthenticateRequest(IOwinContext context)
        {
            var requestHeader = context.Request.Headers.Get("Authorization");
            if (requestHeader != null)
            {
                int userId = Convert.ToInt32(JwtDecoder.GetUserIdFromToken(requestHeader).ToString());
                var identity = new GenericIdentity(userId.ToString(), "StakersClubOwinAuthentication");
                //context.Authentication.User = new ClaimsPrincipal(identity);

                var token = requestHeader.StartsWith("Bearer ") ? requestHeader.Substring(7) : requestHeader;
                var secret = WebConfigurationManager.AppSettings.Get("jwtKey");
                Thread.CurrentPrincipal = ValidateToken(token, secret, true);
                context.Authentication.User = (ClaimsPrincipal)Thread.CurrentPrincipal;
                //if (HttpContext.Current != null)
                //{
                //    HttpContext.Current.User = Thread.CurrentPrincipal;
                //}
            }
        }

        private static ClaimsPrincipal ValidateToken(string token, string secret, bool checkExpiration)
        {
            var jsonSerializer = new JavaScriptSerializer();
            var payloadJson = JsonWebToken.Decode(token, secret);
            var payloadData = jsonSerializer.Deserialize<Dictionary<string, object>>(payloadJson);

            if (payloadData != null && (checkExpiration && payloadData.TryGetValue("exp", out object exp)))
            {
                var validTo = FromUnixTime(long.Parse(exp.ToString()));
                if (DateTime.Compare(validTo, DateTime.UtcNow) <= 0)
                {
                    throw new Exception(string.Format("Token is expired. Expiration: '{0}'. Current: '{1}'", validTo, DateTime.UtcNow));
                }
            }

            var subject = new ClaimsIdentity("Federation", ClaimTypes.Name, ClaimTypes.Role);

            var claims = new List<Claim>();

            if (payloadData != null)
                foreach (var pair in payloadData)
                {
                    var claimType = pair.Key;

                    if (pair.Value is ArrayList source)
                    {
                        claims.AddRange(from object item in source select new Claim(claimType, item.ToString(), ClaimValueTypes.String));
                        continue;
                    }

                    switch (pair.Key)
                    {
                        case "name":
                            claims.Add(new Claim(ClaimTypes.Name, pair.Value.ToString(), ClaimValueTypes.String));
                            break;
                        case "surname":
                            claims.Add(new Claim(ClaimTypes.Surname, pair.Value.ToString(), ClaimValueTypes.String));
                            break;
                        case "email":
                            claims.Add(new Claim(ClaimTypes.Email, pair.Value.ToString(), ClaimValueTypes.String));
                            break;
                        case "role":
                            claims.Add(new Claim(ClaimTypes.Role, pair.Value.ToString(), ClaimValueTypes.String));
                            break;
                        case "userId":
                            claims.Add(new Claim(ClaimTypes.UserData, pair.Value.ToString(), ClaimValueTypes.Integer));
                            break;
                        default:
                            claims.Add(new Claim(claimType, pair.Value.ToString(), ClaimValueTypes.String));
                            break;
                    }
                }

            subject.AddClaims(claims);
            return new ClaimsPrincipal(subject);
        }

        private static DateTime FromUnixTime(long unixTime)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(unixTime);
        }
    }
}