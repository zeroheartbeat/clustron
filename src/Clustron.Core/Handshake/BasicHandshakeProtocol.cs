// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Core.Handshake;
using Clustron.Core.Models;
using Microsoft.Extensions.Logging;

namespace Clustron.Core.Handshake;

public class BasicHandshakeProtocol : IHandshakeProtocol
{
    private readonly string _localVersion;
    private readonly string _localClusterId;
    private readonly ILogger<BasicHandshakeProtocol> _logger;

    public BasicHandshakeProtocol(string version, string clusterId, ILogger<BasicHandshakeProtocol> logger)
    {
        _localVersion = version;
        _localClusterId = clusterId;
        _logger = logger;
    }

    public Task<HandshakeResult> InitiateHandshakeAsync(NodeInfo targetNode)
    {
        _logger.LogInformation($"Attempting handshake with {targetNode.NodeId}");

        if (targetNode.ClusterId != _localClusterId)
        {
            return Task.FromResult(new HandshakeResult
            {
                Accepted = false,
                Reason = "Cluster ID mismatch"
            });
        }

        if (targetNode.Version != _localVersion)
        {
            return Task.FromResult(new HandshakeResult
            {
                Accepted = false,
                Reason = "Version mismatch"
            });
        }

        return Task.FromResult(new HandshakeResult
        {
            Accepted = true,
            Reason = "Accepted"
        });
    }
}

