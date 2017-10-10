﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Lynx.API.Models;
using Lynx.API.ValueConverters;
using Lynx.Core;
using Lynx.Core.Facade.Interfaces;
using Lynx.Core.Interfaces;
using Lynx.Core.Mappers.IDSubsystem.Strategies;
using Lynx.Core.Models.IDSubsystem;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.DependencyInjection;
using Lynx.Core.Facade;
using Nethereum.Web3;
using Attribute = Lynx.Core.Models.IDSubsystem.Attribute;
using Lynx.Core.PeerVerification;
using Lynx.Core.Crypto.Interfaces;
using Lynx.Core.Communications.Packets.Interfaces;
using System.Threading;
using System.Net.Http;
using Lynx.API.Worker.DTO;

namespace Lynx.API.Controllers
{
    [Route("api/[controller]")]
    public class ReadRequestController : Controller
    {
        private ClientContext _clientContext;
        private IServiceProvider _serviceProvider;
        private ID _id;
        private IMapper<ID> _idMapper;
        private IAccountService _accountService;
        private Web3 _web3;
        private InfoRequester _infoRequester;

        public ReadRequestController(ClientContext clientContext, IMapper<ID> idMapper, Web3 web3, IServiceProvider serviceProvider)
        {
            _clientContext = clientContext;
            _idMapper = idMapper;
            _web3 = web3;
            _serviceProvider = serviceProvider;
        }

        // GET api/readrequest
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/readrequest/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/readrequest
        [HttpPost]
        public async Task<IActionResult> Post([FromBody]ReadRequestDTO value)
        {

            if (GetClientByApiKey("1234") == null)
            {
                Client newClient = new Client()
                {
                    APIKey = "1234",
                    IDUID = 1,
                    PrivateKey = "9e6a6bf412ce4e3a91a33c7c0f6d94b3127b8d4f5ed336210a672fe595bf1769"
                };

                _clientContext.Add(newClient);
                _clientContext.SaveChanges();
            }

            Client client;
            if ((client = GetClientByApiKey(value.APIKey)) == null)
                return Unauthorized();


            InfoRequestWorker worker = new InfoRequestWorker(_clientContext, _idMapper, _web3, _serviceProvider);
            InfoRequestSessionDTO dto = await worker.InitiateJob(client);

            /*_infoRequester.handshakeComplete += (sender, e) =>
            {
                new HttpClient().PostAsync(value.CallbackEndpoint, new System.Net.Http.StringContent(e.Id.Address));
            };*/

            BackgroundJob.Enqueue<InfoRequestWorker>(w => w.CompleteJob(dto, value.CallbackEndpoint, null));

            StringToSVGValueConverter valConv = new StringToSVGValueConverter();
            string qrEncodedSyn = valConv.Convert(dto.EncodedSyn).Content;

            ContentResult content = Content(qrEncodedSyn);
            content.ContentType = "image/svg+xml";

            return content;
        }

        // PUT api/readrequest/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/readrequest/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }

        private Client GetClientByApiKey(string apiKey)
        {
            return _clientContext.Clients.SingleOrDefault(c => c.APIKey == apiKey);
        }
    }
}
