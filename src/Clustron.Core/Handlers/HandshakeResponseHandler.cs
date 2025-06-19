// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Abstractions;
using Clustron.Core.Cluster;
using Clustron.Core.Cluster.State;
using Clustron.Core.Handshake;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Serialization;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Clustron.Core.Handlers
{
    public class HandshakeResponseHandler : IMessageHandler
    {
        private readonly IClusterState _clusterLeader;
        private readonly ILogger<HandshakeResponseHandler> _logger;
        private readonly IMessageSerializer _serializer;
        private readonly IClusterRuntime _runtime;

        public HandshakeResponseHandler(IClusterState clusterLeader, IClusterRuntime runtime, IMessageSerializer serializer, ILogger<HandshakeResponseHandler> logger)
        {
            _clusterLeader = clusterLeader;
            _serializer = serializer;  
            _runtime = runtime;
            _logger = logger;
        }

        public string Type => MessageTypes.HandshakeResponse;

        public Task HandleAsync(Message message)
        {
            var response = _serializer.Deserialize<HandshakeResponse>(message.Payload);

            if (response?.Accepted == true)
            {
                _runtime.PeerManager.RegisterPeer(response.ResponderNode);
            }

            if (response?.Leader != null)
            {
                _clusterLeader.ForceLeader(response.Leader, response.LeaderEpoch);
                _logger.LogInformation("Adopting cluster leader from handshake: {LeaderId} (epoch {Epoch})",
                    response.Leader.NodeId, response.LeaderEpoch);
            }

            return Task.CompletedTask;
        }
    }
}

