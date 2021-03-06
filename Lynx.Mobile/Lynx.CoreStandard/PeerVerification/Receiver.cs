using System;
using System.Text;
using Lynx.Core.Communications.Interfaces;
using Lynx.Core.Communications.Packets;
using Lynx.Core.Communications.Packets.Interfaces;
using Lynx.Core.Crypto;
using Lynx.Core.Crypto.Interfaces;
using Lynx.Core.Interfaces;
using Lynx.Core.Models.IDSubsystem;
using Lynx.Core.PeerVerification.Interfaces;
using Attribute = Lynx.Core.Models.IDSubsystem.Attribute;
using Lynx.Core.Facade;
using System.Threading.Tasks;
using Lynx.Core.Communications;
using Lynx.Core.Facade.Interfaces;

namespace Lynx.Core.PeerVerification
{
    public class Receiver : Peer, IReceiver
    {
        private ITokenCryptoService<IToken> _tokenCryptoService;
        private ID _id;
        private IAccountService _accountService;
        private ISession _session;
        private IIDFacade _idFacade;
        private ISynAck _synAck;
        private InfoRequestSynAck _infoRequestSynAck;
        private ISyn _syn;
        private ICertificateFacade _certificateFacade;
        public ISynAck SynAck { get { return _synAck; } }
        public InfoRequestSynAck InfoRequestSynAck { get { return _infoRequestSynAck; } }

        public event EventHandler<IdentityProfileReceivedEvent> IdentityProfileReceived;
        public event EventHandler<InfoRequestReceivedEvent> InfoRequestReceived;

        public Receiver(ITokenCryptoService<IToken> tokenCryptoService, IAccountService accountService, ID id, IIDFacade idFacade, ICertificateFacade certificateFacade) : base(tokenCryptoService, accountService, idFacade)
        {
            _tokenCryptoService = tokenCryptoService;
            _id = id;
            _accountService = accountService;
            _idFacade = idFacade;
            _certificateFacade = certificateFacade;
            _session = new AblySession(new EventHandler<string>(async (sender, e) => await RouteEncryptedHandshakeToken<SynAck>(e)), id.Address);
        }

        /// <summary>
        /// Creates and transmits an ACK in response to a previously processed SYN
        /// </summary>
        private void Acknowledge(ISyn syn)
        {
            Ack ack = new Ack()
            {
                Id = _id,
                PublicKey = _accountService.PublicKey,
                Encrypted = true,
            };

            byte[] requesterPubKey = Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.HexToByteArray(syn.PublicKey);
            _tokenCryptoService.Sign(ack, _accountService.GetPrivateKeyAsByteArray());
            string encryptedToken = _tokenCryptoService.Encrypt(ack, requesterPubKey, _accountService.GetPrivateKeyAsByteArray());
            _session.Open(syn.NetworkAddress);
            _session.Send(encryptedToken);
        }

        public async Task ProcessSyn(string synString)
        {
            HandshakeTokenFactory<Syn> synFactory = new HandshakeTokenFactory<Syn>(_idFacade);
            Syn syn = await synFactory.CreateHandshakeTokenAsync(synString);

            byte[] pubK = Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.HexToByteArray(syn.PublicKey);

            VerifyHandshakeTokenIDOwnership(syn);

            if (_tokenCryptoService.VerifySignature(syn))
            {
                _syn = syn;
                Acknowledge(_syn);
            }
            else
                throw new SignatureDoesntMatchException("The signature was not " +
                                                        "generated by the given " +
                                                        "public Key");
        }

        protected async Task RouteEncryptedHandshakeToken<T>(string encryptedHandshakeToken, ID id = null)
        {
            string[] tokenArr = encryptedHandshakeToken.Split(':');

            switch (tokenArr[0])
            {
                case "synack":
                    await ProcessSynAck(encryptedHandshakeToken);
                    break;

                case "inforeqsynack":
                    await ProcessInfoRequestSynAck(encryptedHandshakeToken);
                    break;

                default:
                    throw new InvalidTokenTypeException("The Token type received is invalid");
            }
        }

        private async Task ProcessSynAck(string encryptedSynAck)
        {
            SynAck unverifiedSynAck = await base.DecryptAndInstantiateHandshakeToken<SynAck>(encryptedSynAck, _syn.Id);

            if (unverifiedSynAck.PublicKey != _syn.PublicKey)
                throw new TokenPublicKeyMismatch();

            if (!_tokenCryptoService.VerifySignature(unverifiedSynAck))
                throw new SignatureDoesntMatchException("The signature was not " +
                                                                        "generated by the given " +
                                                                        "public Key");

            _synAck = unverifiedSynAck;

            IdentityProfileReceivedEvent e = new IdentityProfileReceivedEvent()
            {
                SynAck = _synAck
            };
            IdentityProfileReceived.Invoke(this, e);
        }

        /// <summary>
        /// Processes an InfoRequestSynAck containing requested attributes and fires an event 
        /// </summary>
        private async Task ProcessInfoRequestSynAck(string encryptedInfoRequestToken)
        {
            InfoRequestSynAck infoRequestSynAck = await base.DecryptAndInstantiateHandshakeToken<InfoRequestSynAck>(encryptedInfoRequestToken, _syn.Id);

            if (infoRequestSynAck.PublicKey != _syn.PublicKey)
                throw new TokenPublicKeyMismatch();

            if (_tokenCryptoService.VerifySignature(infoRequestSynAck))
            {
                _infoRequestSynAck = infoRequestSynAck;

                //this event is fired to display an activity showing the requested attributes
                InfoRequestReceivedEvent e = new InfoRequestReceivedEvent()
                {
                    InfoRequestSynAck = _infoRequestSynAck
                };
                InfoRequestReceived.Invoke(this, e);
            }
            else
                throw new SignatureDoesntMatchException("The signature was not " +
                                                                        "generated by the given " +
                                                                        "public Key");
        }

        /// <summary>
        /// JSON-Encodes and sends attributes and attribute contents to the requesting service
        /// </summary>
        public void AuthorizeReadRequest(string[] keysOfAttributesToAuthorize)
        {
            Attribute[] authorizedAttr = new Attribute[keysOfAttributesToAuthorize.Length];
            for (int i = 0; i < keysOfAttributesToAuthorize.Length; i++)
            {
                authorizedAttr[i] = _id.Attributes[keysOfAttributesToAuthorize[i]];
            }

            InfoRequestResponse response = new InfoRequestResponse()
            {
                Id = _id,
                PublicKey = _accountService.PublicKey,
                Encrypted = true,
                AccessibleAttributes = authorizedAttr
            };

            byte[] requesterPubKey = Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.HexToByteArray(_infoRequestSynAck.PublicKey);
            _tokenCryptoService.Sign(response, _accountService.GetPrivateKeyAsByteArray());
            string encryptedToken = _tokenCryptoService.Encrypt(response, requesterPubKey, _accountService.GetPrivateKeyAsByteArray());
            _session.Send(encryptedToken);
        }


        public async Task Certify(string[] keysOfAttributesToCertifify)
        {
            Certificate[] certificates = await IssueCertificates(keysOfAttributesToCertifify);

            string encryptedToken = CreateEncryptedCertificationConfirmationToken(certificates);

            _session.Send(encryptedToken);
        }

        private string CreateEncryptedCertificationConfirmationToken(Certificate[] certificates)
        {
            CertificationConfirmationToken certConfToken = new CertificationConfirmationToken()
            {

                PublicKey = _accountService.PublicKey,
                Encrypted = true,
                IssuedCertificates = certificates
            };

            byte[] requesterPubKey = Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.HexToByteArray(SynAck.PublicKey);
            _tokenCryptoService.Sign(certConfToken, _accountService.GetPrivateKeyAsByteArray());
            return _tokenCryptoService.Encrypt(certConfToken, requesterPubKey, _accountService.GetPrivateKeyAsByteArray());
        }

        private async Task<Certificate[]> IssueCertificates(string[] attributeKeys)
        {
            Certificate[] certificates = new Certificate[attributeKeys.Length];

            int i = 0;
            foreach (string key in attributeKeys)
            {
                Attribute attr = SynAck.Id.Attributes[key];
                Certificate cert = new Certificate()
                {
                    OwningAttribute = attr,
                    Revoked = false,
                    Location = "CertFor" + attr.Description,
                    Hash = "HashFor" + attr.Description
                };

                await _certificateFacade.DeployAsync(cert);

                certificates[i] = cert;
                i++;
            }

            return certificates;
        }
    }
}
