﻿/*
 * Copyright (c) Dominick Baier.  All rights reserved.
 * see license.txt
 */

using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IdentityModel.Tokens;
using System.Linq;
using System.Security.Claims;
using Thinktecture.IdentityServer.Repositories;

namespace Thinktecture.IdentityServer.TokenService
{
    public class RepositoryX509SecurityTokenHandler : X509SecurityTokenHandler
    {
        [Import]
        public IUserRepository UserRepository { get; set; }

        static Logger logger = LogManager.GetCurrentClassLogger();

        public override ReadOnlyCollection<ClaimsIdentity> ValidateToken(SecurityToken token)
        {
            logger.Info("Beginning client certificate token validation and authentication for SOAP");
            Container.Current.SatisfyImportsOnce(this);
            
            // call base class implementation for validation and claims generation 
            var identity = base.ValidateToken(token).First();

            // retrieve thumbprint
            var clientCert = ((X509SecurityToken)token).Certificate;
            logger.Info(String.Format("Client certificate thumbprint: {0}", clientCert.Thumbprint));

            // check if mapped user exists
            string userName;
            if (!UserRepository.ValidateUser(clientCert, out userName))
            {
                var message = String.Format("No mapped user exists for thumbprint {0}", clientCert.Thumbprint);
                logger.Error(message);
                throw new SecurityTokenValidationException(message);
            }

            logger.Info(String.Format("Mapped user found: {0}", userName));

            // retrieve issuer name
            var issuer = identity.Claims.First().Issuer;
            logger.Info(String.Format("Certificate issuer: {0}", issuer));

            // create new ClaimsIdentity for the STS issuance logic
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, userName),
                new Claim(ClaimTypes.AuthenticationMethod, AuthenticationMethods.X509),
                new Claim(ClaimTypes.AuthenticationInstant, identity.FindFirst(ClaimTypes.AuthenticationInstant).Value)
            };

            var id = new ClaimsIdentity(claims, "Client Certificate");
            return new List<ClaimsIdentity> { id }.AsReadOnly();
        }
    }
}