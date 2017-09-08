using Lynx.Core.Communications.Packets.Interfaces;
using System.Threading.Tasks;
using System;

namespace Lynx.Core.PeerVerification.Interfaces
{
    /// <summary>
    /// IVerifiers is in charge of handling the verification request coming from
    /// other peers. It will process the Syn request and verify the data supplied
    /// by the peer.
    /// </summary>
    public interface IVerifier
    {
        event EventHandler<IdentityProfileReceivedEvent> IdentityProfileReceived;
    }
}
