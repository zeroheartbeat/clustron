// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

// File: Interfaces/IClustronClient.cs
using Clustron.Client;
using Clustron.Client.Models;
using Clustron.Core.Messaging;

namespace Clustron.Client;

public interface IClustronClient
{
    // Messaging
    Task SendAsync<T>(string nodeId, T payload);
    Task BroadcastAsync<T>(T payload);

    ClusterNode Self { get; }

    // Cluster events
    event Action<ClusterNode> NodeJoined;
    event Action<ClusterNode> NodeLeft;
    event Action<ClusterNode, int> LeaderChanged;

    // Cluster state
    ClusterNode? GetCurrentLeader();
    IEnumerable<ClusterNode> GetPeers();
    IEnumerable<ClusterNode> GetPeersByRole(string role);
    bool IsPeerAlive(string nodeId);

    //void OnMessageReceived<T>(Func<ClientPayload<T>, Task> handler);
    void OnMessageReceived<T>(Func<T, string, Task> handler);

    bool TryGetHandler(string messageType, out Func<byte[], string, Task> dispatcher);
}

