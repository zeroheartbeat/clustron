// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Core.Messaging;
using Clustron.Core.Models;

namespace Clustron.Core.Handshake;

public interface IHandshakeProtocol
{
    Task<HandshakeResponse> InitiateHandshakeAsync(NodeInfo targetNode);  // For outbound requests
    Task<HandshakeResponse> ProcessHandshake(HandshakeRequest request); // For inbound handling
}

