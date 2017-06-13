﻿using Lynx.Core.Facade.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Web3;
using Lynx.Core.Models.IDSubsystem;
using eVi.abi.lib.pcl;
using Nethereum.Hex.HexTypes;
using System.Numerics;

namespace Lynx.Core.Facade
{
    class IDFacade : Facade, IIDFacade
    {
        string _factoryAddress;

        public IDFacade(string address, string password, string factoryAddress, Web3 web3) : base(address, password, web3)
        {
            _factoryAddress = factoryAddress;
        }

        public IDFacade(string address, string password, string factoryAddress) : base(address, password, new Web3())
        {
            _factoryAddress = factoryAddress;
        }

        public IDFacade(string address, string password) : base(address, password, new Web3())
        {
        }

        public async Task<ID> DeployAsync(ID id)
        {
            FactoryService factory = new FactoryService(_web3, _factoryAddress);
            ID newID = new ID();
            Event idCreationEvent = factory.GetEventReturnIDController();
            HexBigInteger filterAddressFrom = await idCreationEvent.CreateFilterAsync(_address);

            string transactionHash = await factory.CreateIDAsync(_address);
            string transactionHash2 = await factory.CreateIDAsync(_address);

            var log = await idCreationEvent.GetFilterChanges<ReturnIDControllerEventDTO>(filterAddressFrom);

            string controllerAddress = log[0].Event._controllerAddress;

            newID.Address = controllerAddress;

            //Add each attribute from the ID model to the ID smart contract
            IAttributeFacade ethAttribute = new AttributeFacade(_address, _password, _web3);
            foreach (string key in id.GetAttributeKeys())
            {
                Attribute attribute = id.GetAttribute(key);

                //Should only happen if the attribute does not match the desired type 
                if (attribute == null) continue;

                attribute = await AddAttributeAsync(newID, Encoding.UTF8.GetBytes(key), attribute);
                newID.AddAttribute(attribute);
            }
            return id;
        }

        public async Task<ID> GetIDAsync(string address)
        {
            ID newID = new ID();
            newID.Address = address;

            //Get all attributes from the smart contract and add them to the ID model
            Dictionary<byte[], Attribute> attributes = await GetAttributesAsync(newID);
            foreach (Attribute attr in attributes.Values)
            {
                newID.AddAttribute(attr);
            }

            return newID;

        }

        public async Task<Attribute> AddAttributeAsync(ID id, byte[] key, Attribute attribute)
        {
            IDControllerService ethIDCtrl = new IDControllerService(_web3, id.Address);
            IAttributeFacade AttributeFacade = new AttributeFacade(_address, _password, _web3);

            //If the attribute to be added is not yet deployed, deploy it
            if (attribute.Address == "")
                attribute = await AttributeFacade.DeployAsync(attribute);

            await ethIDCtrl.AddAttributeAsync(_address, key, attribute.Address);

            return attribute;
        }

        public async Task<Dictionary<byte[], Attribute>> GetAttributesAsync(ID id)
        {
            IDControllerService ethIdCtrl = new IDControllerService(_web3, id.Address);
            Dictionary<byte[], Attribute> dict = new Dictionary<byte[], Attribute>();

            BigInteger attributes = await ethIdCtrl.AttributeCountAsyncCall();
            for (BigInteger i = 0; i < attributes; i++)
            {
                //Get all attribute keys and addresses for the ID
                byte[] attributeKey = await ethIdCtrl.GetAttributeKeyAsyncCall(i);
                string ethAttributeAddress = await ethIdCtrl.GetAttributeAsyncCall(attributeKey);
                AttributeFacade attributeFacade = new AttributeFacade(_address, _password, _web3);
                //Get the attribute and add it to the local ID model
                Attribute newAttribute = await attributeFacade.GetAttributeAsync(ethAttributeAddress);
                dict.Add(attributeKey, newAttribute);
            }

            return dict;
        }

    }
}
