﻿using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace Lynx.API.Models
{
    public class Client : IdentityUser
    {
        public string APIKey { get; set; }
        public int IDUID { get; set; }
        public string PrivateKey { get; set; }
    }
}
