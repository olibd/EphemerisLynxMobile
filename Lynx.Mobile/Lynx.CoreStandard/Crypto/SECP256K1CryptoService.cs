using System;
using Lynx.Core.Crypto.Interfaces;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Agreement.Kdf;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Security;

namespace Lynx.Core.Crypto
{
    public class SECP256K1CryptoService : IECCCryptoService
    {
        private string _curveName = "secp256k1";
        private X9ECParameters _ecP;
        private ECDomainParameters _ecSpec;

        public SECP256K1CryptoService()
        {
            _ecP = SecNamedCurves.GetByName(_curveName);
            _ecSpec = new ECDomainParameters(_ecP.Curve, _ecP.G, _ecP.N, _ecP.H, _ecP.GetSeed());
        }

        public ECPublicKeyParameters GeneratePublicKey(byte[] pubkey)
        {
            ECPoint point = _ecSpec.Curve.DecodePoint(pubkey);
            ECPublicKeyParameters publicKey = new ECPublicKeyParameters("ECDH", point, _ecSpec);
			return publicKey;
        }

        public ECPrivateKeyParameters GeneratePrivateKey(byte[] privkey)
        {
            string privKeyString = Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(privkey);
            ECPrivateKeyParameters privateKey = new ECPrivateKeyParameters("ECDH", new BigInteger(privKeyString, 16), _ecSpec);
            return privateKey;
        }

        public byte[] GetSharedSecretValue(ECPublicKeyParameters publicKey, ECPrivateKeyParameters privateKey)
        {
            ECDHCBasicAgreement eLacAgreement = new ECDHCBasicAgreement();
            eLacAgreement.Init(privateKey);
            BigInteger eLA = eLacAgreement.CalculateAgreement(publicKey);
            byte[] eLABytes = eLA.ToByteArray(); 
            return eLABytes;
		}

        /// <summary>
        /// Derives the symmetric key from the shared secret.
        /// </summary>
        /// <returns>The symmetric key.</returns>
        /// <param name="sharedSecret">Shared secret.</param>
        private byte[] DeriveSymmetricKeyFromSharedSecret(byte[] sharedSecret)
        {
            ECDHKekGenerator egH = new ECDHKekGenerator(DigestUtilities.GetDigest("SHA256"));
            egH.Init(new DHKdfParameters(NistObjectIdentifiers.Aes, sharedSecret.Length, sharedSecret));
            byte[] symmetricKey = new byte[DigestUtilities.GetDigest("SHA256").GetDigestSize()];
            egH.GenerateBytes(symmetricKey, 0, symmetricKey.Length);
            return symmetricKey;
        }

        public byte[] Encrypt(byte[] data, byte[] pubkey, byte[] privkey)
        {
            byte[] output = null;
            ECPublicKeyParameters publicKey = GeneratePublicKey(pubkey);
            ECPrivateKeyParameters privateKey = GeneratePrivateKey(privkey);
            byte[] sharedSecret = GetSharedSecretValue(publicKey, privateKey);
            byte[] derivedKey = DeriveSymmetricKeyFromSharedSecret(sharedSecret);

            KeyParameter keyparam = ParameterUtilities.CreateKeyParameter("AES", derivedKey);
            IBufferedCipher cipher = CipherUtilities.GetCipher("AES/CBC/PKCS7PADDING");
            cipher.Init(true, keyparam);

            try
            {
                output = cipher.DoFinal(data,0,data.Length);
                return output;
            }
            catch (InvalidCipherTextException ex)
            {
                throw new CryptoException("Invalid Data");
            }
            catch (DataLengthException ex)
            {
                throw new CryptoException("Invalid Data");
            }
        }

        public byte[] Decrypt(byte[] cipherData, byte[] pubkey, byte[] privkey)
        {
            byte[] output = null;
            ECPublicKeyParameters publicKey = GeneratePublicKey(pubkey);
            ECPrivateKeyParameters privateKey = GeneratePrivateKey(privkey);
            byte[] sharedSecret = GetSharedSecretValue(publicKey, privateKey);
            byte[] derivedKey = DeriveSymmetricKeyFromSharedSecret(sharedSecret);

            KeyParameter keyparam = ParameterUtilities.CreateKeyParameter("AES", derivedKey);
            IBufferedCipher cipher = CipherUtilities.GetCipher("AES/CBC/PKCS7PADDING");
            cipher.Init(false, keyparam);

            try
            {
                output = cipher.DoFinal(cipherData, 0, cipherData.Length);
                return output;
            }
            catch (InvalidCipherTextException ex)
            {
                throw new CryptoException("Invalid Data");
            }
            catch (DataLengthException ex)
            {
                throw new CryptoException("Invalid Data");
            }
        }

        public bool VerifySignedData(byte[] data, byte[] signature, byte[] pubkey)
        {
            throw new NotImplementedException();
        }

        public byte[] GetDataSignature(byte[] data, byte[] privateKey)
        {
            throw new NotImplementedException();
        }
    }
}

