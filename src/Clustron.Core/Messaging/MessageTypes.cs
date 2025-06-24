// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

namespace Clustron.Core.Messaging
{
    public static class MessageTypes
    {
        public const string HandshakeRequest = "clustron.handshake.request";
        public const string HandshakeResponse = "clustron.handshake.response";
        public const string LeaderChanged = "clustron.leader.changed";
        public const string NodeJoined = "clustron.node.joined";
        public const string NodeLeft = "clustron.node.left";
        public const string Heartbeat = "clustron.heartbeat";
        public const string HeartbeatSuspect = "clustron.heartbeat.suspect";
        public const string ClustronMetrics = "clustron.metrics"; 
        public const string RequestMetrics = "clustron.metrics.request";
        public const string ClusterView = "clustron.cluster.view";
        public const string CustomEvent = "clustron.custom.event";
    }
}

