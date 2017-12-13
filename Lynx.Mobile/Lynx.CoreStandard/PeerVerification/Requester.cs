using System;
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
using Lynx.Core.Mappers.IDSubsystem.Strategies;
using Lynx.Core.Models.IDSubsystem;
using Lynx.Core.PeerVerification.Interfaces;
using Attribute = Lynx.Core.Models.IDSubsystem.Attribute;

namespace Lynx.Core.PeerVerification
{

    public class Requester : Peer, IRequester
    {
        protected ISession _session;
        private ID _id;
        protected ITokenCryptoService<IToken> _tokenCryptoService;
        protected IAccountService _accountService;
        private ICertificateFacade _certificateFacade;
        protected Attribute[] _accessibleAttributes;
        private IAttributeFacade _attributeFacade;
        public event EventHandler<IssuedCertificatesAddedToIDEvent> HandshakeComplete;

        public Requester(ITokenCryptoService<IToken> tokenCryptoService, IAccountService accountService, ID id, IIDFacade idFacade, IAttributeFacade attributeFacade, ICertificateFacade certificateFacade) : base(tokenCryptoService, accountService, idFacade)
        {
            _tokenCryptoService = tokenCryptoService;
            _accountService = accountService;
            _session = new AblySession(new EventHandler<string>(async (sender, e) => await RouteEncryptedHandshakeToken<Ack>(e)), id.Address);
            _id = id;
            _attributeFacade = attributeFacade;
            _certificateFacade = certificateFacade;
            _accessibleAttributes = new Attribute[_id.Attributes.Values.Count];
            _id.Attributes.Values.CopyTo(_accessibleAttributes, 0);
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
        protected virtual void GenerateAndSendSynAck(Ack ack)
        {
            SynAck synAck = new SynAck()
            {
                Id = _id,
                PublicKey = _accountService.PublicKey,
                Encrypted = true,
                AccessibleAttributes = _accessibleAttributes
            };

            try
            {
                byte[] requesterPubKey = Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions
                    .HexToByteArray(ack.PublicKey);
                _tokenCryptoService.Sign(synAck, _accountService.GetPrivateKeyAsByteArray());
                string encryptedToken =
                    _tokenCryptoService.Encrypt(synAck, requesterPubKey, _accountService.GetPrivateKeyAsByteArray());
                _session.Send(encryptedToken);
            }
            catch (Exception e)
            {
                throw new SynAckFailedException("Unable to respond to the other peer", e);
            }
        }

        protected virtual async Task RouteEncryptedHandshakeToken<T>(string encryptedHandshakeToken)
        {
            try
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
                        throw new InvalidTokenTypeException("The other peer sent invalid data");
                }
            }
            catch (UserFacingException e)
            {
                RaiseError(e);
            }
        }

        protected async Task ProcessAck(string encryptedToken)
        {
            Ack ack = await base.DecryptAndInstantiateHandshakeToken<Ack>(encryptedToken);
            VerifyHandshakeTokenIDOwnership(ack);

            try
            {
                if (_tokenCryptoService.VerifySignature(ack))
                {
                    Ack = ack;
                    GenerateAndSendSynAck(ack);
                }
                else
                    throw new SignatureMismatchException("Unable to validate the other peer's signature");
            }
            catch (UserFacingException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new UnableToProcessTokenException("The other user sent invalid data", e);
            }
        }

        private async Task ProcessCertificationConfirmationToken(string encryptedToken)
        {
            string decryptedToken = _tokenCryptoService.Decrypt(encryptedToken, _accountService.GetPrivateKeyAsByteArray());
            CertificationConfirmationTokenFactory tokenFactory = new CertificationConfirmationTokenFactory(_certificateFacade);
            CertificationConfirmationToken token = await tokenFactory.CreateTokenAsync(decryptedToken);

            if (token.PublicKey != Ack.PublicKey)
                throw new TokenPublicKeyMismatchException();

            if (_tokenCryptoService.VerifySignature(token))
                await AddCertificatesToTheAccessibleAttributes(token.IssuedCertificates);
            else
                throw new SignatureMismatchException("Unable to validate the other peer's signature");
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

                    //overwrite the old certificate
                    if (attr.Certificates.ContainsKey(cert.Owner))
                        attr.Certificates.Remove(cert.Owner);

                    attr.AddCertificate(cert);

                    await _attributeFacade.AddCertificateAsync(attr, cert);
                    addedCertificate.Add(cert);
                }
            }
            _session.Close();
            IssuedCertificatesAddedToIDEvent e = new IssuedCertificatesAddedToIDEvent()
            {
                CertificatesAdded = addedCertificate
            };
            HandshakeComplete.Invoke(this, e);
        }

        public void ResumeSession(string sessionID)
        {
            _session.Open(sessionID);
        }

        public string SuspendSession()
        {
            string sId = _session.SessionID;
            _session.Close();
            return sId;
        }
    }
}
