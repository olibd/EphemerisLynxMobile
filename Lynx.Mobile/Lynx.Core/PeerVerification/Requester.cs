﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Lynx.Core.Communications;
using Lynx.Core.Communications.Interfaces;
using Lynx.Core.Communications.Packets;
using Lynx.Core.Communications.Packets.Interfaces;
using Lynx.Core.Crypto;
using Lynx.Core.Crypto.Interfaces;
using Lynx.Core.Facade;
using Lynx.Core.Facade.Interfaces;
using Lynx.Core.Interfaces;
using Lynx.Core.Models.IDSubsystem;
using Lynx.Core.PeerVerification.Interfaces;
using Attribute = Lynx.Core.Models.IDSubsystem.Attribute;

namespace Lynx.Core.PeerVerification
{

    public class Requester : Peer, IRequester
    {
        private ISession _session;
        private ID _id;
        private ITokenCryptoService<IToken> _tokenCryptoService;
        private IAccountService _accountService;
        private ICertificateFacade _certificateFacade;
        private Attribute[] _accessibleAttributes;
        private IAttributeFacade _attributeFacade;
        //prevents the early calling of the ProcessCertificationConfirmationToken event handler
        private bool _synAckSent = false;
        public event EventHandler<IssuedCertificatesAddedToIDEvent> IssuedCertificatesAddedToID;

        public Requester(ITokenCryptoService<IToken> tokenCryptoService, IAccountService accountService, ID id, IIDFacade idFacade, IAttributeFacade attributeFacade, ICertificateFacade certificateFacade) : base(tokenCryptoService, accountService, idFacade)
        {
            _tokenCryptoService = tokenCryptoService;
            _accountService = accountService;
            _session = new PubNubSession(new EventHandler<string>(async (sender, e) => await ProcessEncryptedHandshakeToken<Ack>(e)));
            _session.AddMessageReceptionHandler(new EventHandler<string>(async (sender, e) => await ProcessCertificationConfirmationToken(e)));
            _id = id;
            _attributeFacade = attributeFacade;
            _certificateFacade = certificateFacade;
            _accessibleAttributes = new Attribute[]{
                _id.Attributes["firstname"],
                _id.Attributes["lastname"],
                _id.Attributes["cell"],
                _id.Attributes["address"]
            };
        }

        public IAck Ack { get; set; }

        public string CreateEncodedSyn()
        {
            ISyn syn = new Syn()
            {
                Encrypted = false,
                PublicKey = _accountService.PublicKey,
                NetworkAddress = _session.Open(),
                Id = _id
            };

            _tokenCryptoService.Sign(syn, _accountService.GetPrivateKeyAsByteArray());

            return syn.GetEncodedToken();
        }

        /// <summary>
        /// JSON-Encodes and sends attributes and attribute contents to the verifier for certification
        /// </summary>
        private void GenerateAndSendSynAck(Ack ack)
        {
            SynAck synAck = new SynAck()
            {
                Id = _id,
                PublicKey = _accountService.PublicKey,
                Encrypted = true,
                AccessibleAttributes = _accessibleAttributes
            };

            byte[] requesterPubKey = Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.HexToByteArray(ack.PublicKey);
            string encryptedToken = _tokenCryptoService.Encrypt(synAck, requesterPubKey, _accountService.GetPrivateKeyAsByteArray());
            _session.Send(encryptedToken);
            _synAckSent = true;
        }

        protected override async Task<T> ProcessEncryptedHandshakeToken<T>(string encryptedHandshakeToken)
        {
            string[] tokenArr = encryptedHandshakeToken.Split(':');

            switch (tokenArr[0])
            {
                case "ack":
                    await ProcessAck(encryptedHandshakeToken);
                    break;

                case "cert":
                    await ProcessCertificationConfirmationToken(encryptedHandshakeToken);
                    break;

                default:
                    throw new InvalidTokenType("The Token type received is invalid");
            }

            //We can return null because the caller of this method is an anonymous method in an EventHandler
            //and it won't use the returned data
            return null;
        }

        private async Task ProcessAck(string encryptedToken)
        {
            Ack ack = await base.ProcessEncryptedHandshakeToken<Ack>(encryptedToken);

            if (_tokenCryptoService.VerifySignature(ack))
                GenerateAndSendSynAck(ack);
            else
                throw new SignatureDoesntMatchException("The signature was not " +
                                                        "generated by the given " +
                                                        "public Key");
        }

        private async Task ProcessCertificationConfirmationToken(string encryptedToken)
        {
            string decryptedToken = _tokenCryptoService.Decrypt(encryptedToken, _accountService.GetPrivateKeyAsByteArray());
            CertificationConfirmationTokenFactory tokenFactory = new CertificationConfirmationTokenFactory(_certificateFacade);
            CertificationConfirmationToken token = await tokenFactory.CreateTokenAsync(decryptedToken);

            if (_tokenCryptoService.VerifySignature(token))
                await AddCertificatesToTheAccessibleAttributes(token.IssuedCertificates);
            else
                throw new SignatureDoesntMatchException("The signature was not " +
                                                        "generated by the given " +
                                                        "public Key");
        }

        private async Task AddCertificatesToTheAccessibleAttributes(Certificate[] certificates)
        {
            List<Certificate> addedCertificate = new List<Certificate>();
            foreach (Attribute attr in _accessibleAttributes)
            {
                foreach (Certificate cert in certificates)
                {
                    if (attr.Address != cert.OwningAttribute.Address)
                        continue;

                    cert.OwningAttribute = attr;
                    attr.AddCertificate(cert);

                    await _attributeFacade.AddCertificateAsync(attr, cert);
                    addedCertificate.Add(cert);
                }
            }

            IssuedCertificatesAddedToIDEvent e = new IssuedCertificatesAddedToIDEvent()
            {
                CertificatesAdded = addedCertificate,
            };

            IssuedCertificatesAddedToID.Invoke(this, e);
        }
    }
}
