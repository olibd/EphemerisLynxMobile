﻿using System;
using Lynx.Core;
using Lynx.Core.Models.IDSubsystem;
using NUnit.Framework;
using Attribute = Lynx.Core.Models.IDSubsystem.Attribute;

namespace CoreUnitTests.PCL
{
    public abstract class HandshakeTokenTest : TokenTest
    {
        protected string _privateKey;
        protected ID _id;
        protected AccountService _accountService;

        public override void Setup()
        {
            base.Setup();
            /////////////////////
            //Create a dummy ID//
            /////////////////////

            //create some dummy attributes
            Attribute firstname = new Attribute()
            {
                Location = "1",
                Hash = "1",
                Content = new StringContent("Olivier"),
                Description = "firstname"
            };

            Attribute lastname = new Attribute()
            {
                Location = "2",
                Hash = "2",
                Content = new StringContent("Brochu Dufour"),
                Description = "lastname"
            };

            Attribute age = new Attribute()
            {
                Location = "3",
                Hash = "3",
                Content = new IntContent(24),
                Description = "age"
            };

            _id = new ID();
            _id.AddAttribute(firstname);
            _id.AddAttribute(lastname);
            _id.AddAttribute(age);

            /////////////////////////
            //Create an eth account//
            /////////////////////////
            _privateKey = "9e6a6bf412ce4e3a91a33c7c0f6d94b3127b8d4f5ed336210a672fe595bf1769";
            _accountService = new AccountService(_privateKey);

            //////////////////////////////////////////////
            //Add to the header and footers dictionaries//
            //////////////////////////////////////////////

            _header.Add("pubkey", _accountService.PublicKey);
            _header.Add("encrypted", "False");
            _payload.Add("idAddr", _id.Address);
        }
    }
}
