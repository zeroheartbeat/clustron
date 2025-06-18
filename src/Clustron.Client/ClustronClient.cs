// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Client.Internals;
using Clustron.Client.Models;
using Clustron.Core.Client;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Clustron.Client;

public class ClustronClient : IClustronClient
{
    private readonly ClustronClientCore _core;

    public event Action<ClusterNode>? NodeJoined;
    public event Action<ClusterNode>? NodeLeft;
    public event Action<ClusterNode, int>? LeaderChanged;

    private readonly Dictionary<string, Func<byte[], string, Task>> _typedDispatchers = new();

    public ClustronClient(ClustronClientCore core)
    {
        _core = core;

        // Map Core events to client-visible types
        _core.NodeJoined += node => NodeJoined?.Invoke(ToClientNode(node));
        _core.NodeLeft += node => NodeLeft?.Invoke(ToClientNode(node));
        _core.LeaderChanged += (leader, epoch) => LeaderChanged?.Invoke(ToClientNode(leader), epoch);
    }

    public void OnMessageReceived<T>(Func<T, string, Task> handler)
    {
        var messageType = ClientMessageTypes.ClientMessage;

        _typedDispatchers[messageType] = async (payloadBytes, senderId) =>
        {
            var clientPayload = _core.Serializer.Deserialize<ClientPayload<T>>(payloadBytes);
            await handler(clientPayload.Data, senderId);
        };
    }

    public ClusterNode Self => ToClientNode(_core.Self);

    public async Task SendAsync<T>(string nodeId, T data)
    {
        var payload = ClientMessageBuilder.Create<T>(data);

        var message = MessageBuilder.Create<ClientPayload<T>>(
        _core.Self.NodeId,                       // senderId
        ClientMessageTypes.ClientMessage,        // type
        Guid.NewGuid().ToString(),               // correlationId
        payload);

        await _core.SendAsync(message, nodeId);
    }

    public async Task BroadcastAsync<T>(T data)
    {
        var payload = ClientMessageBuilder.Create<T>(data);
        var message = MessageBuilder.Create<ClientPayload<T>>(
        _core.Self.NodeId,                       // senderId
        ClientMessageTypes.ClientMessage,        // type
        Guid.NewGuid().ToString(),               // correlationId
        payload);

        await _core.BroadcastAsync(message);
    }

    public ClusterNode? GetCurrentLeader()
        => ToClientNode(_core.GetCurrentLeader());

    public IEnumerable<ClusterNode> GetPeers()
        => _core.GetMembers().Select(ToClientNode);

    public IEnumerable<ClusterNode> GetPeersByRole(string role)
        => _core.GetPeersByRole(role).Select(ToClientNode);

    public bool IsPeerAlive(string nodeId)
        => _core.IsPeerAlive(nodeId);

    public bool TryGetHandler(string messageType, out Func<byte[], string, Task> dispatcher)
    {
        return _typedDispatchers.TryGetValue(messageType, out dispatcher);
    }

    private static ClusterNode ToClientNode(NodeInfo node)
    {
        return new ClusterNode
        {
            NodeId = node.NodeId,
            Host = node.Host,
            Port = node.Port,
            Roles = node.Roles,
        };
    }
}

