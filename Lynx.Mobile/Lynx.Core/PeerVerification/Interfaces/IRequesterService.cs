﻿using System.Collections.Generic;
using Lynx.Core.Communications.Packets.Interfaces;
using Attribute = Lynx.Core.Models.IDSubsystem.Attribute;

namespace Lynx.Core.PeerVerification.Interfaces
{
    /// <summary>
    /// IRequesterService is in charge of initiating the peer to peer verification
    /// requests. It is a representation of the Requesting entity in a peer to
    /// peer verification transaction. As such, the Requester will also be in
    /// charge of sending the data to be verified the the other party, the 
    /// Verifier.
    /// </summary>
    public interface IRequesterService
    {
        /// <summary>
        /// The Ack object populated by ProcessAck
        /// </summary>
        IAck Ack { get; set; }

        /// <summary>
        /// Creates a verification request SYN from the user's ID and encodes it into a JSON string
        /// </summary>
        /// <returns>A JSON string representing the SYN payload</returns>
        string CreateEncodedSyn();

        /// <summary>
        /// Parses a JSON-encoded ACK and verifies its integrity.
        /// </summary>
        /// <param name="ack">The JSON-encoded ACK</param>
        /// <returns>The Ack object</returns>
        IAck ProcessEncodedAck(string ack);

        /// <summary>
        /// JSON-Encodes and sends attributes and attribute contents to the verifier for certification
        /// </summary>
        /// <param name="attributes">The attributes to transmit</param>
        void SendAttributes(List<Attribute> attributes);

    }
}
