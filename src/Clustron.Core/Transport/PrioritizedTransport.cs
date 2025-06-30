using Clustron.Abstractions;
using Clustron.Core.Cluster;
using Clustron.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Core.Transport;

public class PrioritizedTransport : ITransport
{
    private readonly BaseTcpTransport _inner;
    private readonly ClusterPeerManager _peerManager;
    private readonly PriorityTransportQueue _queue;

    public PrioritizedTransport(BaseTcpTransport inner, ClusterPeerManager peerManager)
    {
        _inner = inner;
        _peerManager = peerManager;

        _queue = new PriorityTransportQueue(async msg =>
        {
            try
            {
                if (msg.TargetNodeId != null)
                {
                    var target = _peerManager.GetPeerById(msg.TargetNodeId);
                    if (target != null)
                        await _inner.SendAsync(target, msg.Message);
                }
                else
                {
                    await _inner.BroadcastAsync(msg.Message, false, msg.Roles);
                }
            }
            catch (Exception ex)
            {
                // Optional: log send failures or update metrics
            }
        });
    }

    public Task SendAsync(NodeInfo target, Message message)
    {
        var priority = MessageClassifier.GetPriority(message.MessageType);
        return _queue.EnqueueAsync(new PrioritizedMessage
        {
            Message = message,
            Priority = priority,
            TargetNodeId = target.NodeId
        });
    }

    public Task BroadcastAsync(Message message, bool sendImmediate, string[] roles = null)
    {
        var priority = MessageClassifier.GetPriority(message.MessageType);
        return _queue.EnqueueAsync(new PrioritizedMessage
        {
            Message = message,
            Priority = priority,
            Roles = roles
        });
    }

    public Task SendImmediateAsync(NodeInfo target, Message message) => _inner.SendImmediateAsync(target, message);
    public Task<bool> CanReachNodeAsync(NodeInfo node) => _inner.CanReachNodeAsync(node);
    public void RemoveConnection(string nodeId) => _inner.RemoveConnection(nodeId);
    public Task StartAsync(IMessageRouter router) => _inner.StartAsync(router);

    public Task<Message> WaitForResponseAsync(string expectedSenderId, string correlationId, TimeSpan timeout)
    {
        return _inner.WaitForResponseAsync(expectedSenderId, correlationId, timeout);  
    }

    public Task HandlePeerDownAsync(string nodeId)
    {
        _inner.HandlePeerDownAsync(nodeId);
        return Task.CompletedTask;
    }
}

