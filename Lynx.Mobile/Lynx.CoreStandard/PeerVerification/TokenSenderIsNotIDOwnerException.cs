﻿using System;

namespace Lynx.Core.PeerVerification
{
    internal class TokenSenderIsNotIDOwnerException : UserFacingException
    {
        public TokenSenderIsNotIDOwnerException()
        {
        }

        public TokenSenderIsNotIDOwnerException(string message) : base(message)
        {
        }

        public TokenSenderIsNotIDOwnerException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}