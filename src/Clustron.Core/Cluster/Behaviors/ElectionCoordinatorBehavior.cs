// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Core.Cluster.Behaviors;
using Clustron.Core.Configuration;
using Clustron.Core.Election;
using Clustron.Core.Events;
using Clustron.Core.Health;
using Clustron.Core.Lifecycle;
using Clustron.Core.Models;
using Microsoft.Extensions.Logging;

public class ElectionCoordinatorBehavior : IRoleAwareBehavior
{
    private readonly IHeartbeatMonitor _heartbeat;
    private readonly ILeaderElectionService _electionService;
    private readonly ILogger<ElectionCoordinatorBehavior> _logger;
    private readonly IClusterEventBus _eventBus;


    public string Name => "ElectionCoordinator";

    public ElectionCoordinatorBehavior(
        IHeartbeatMonitor heartbeat,
        ILeaderElectionService electionService,
        IClusterEventBus eventBus,
        ILogger<ElectionCoordinatorBehavior> logger)
    {
        _heartbeat = heartbeat;
        _electionService = electionService;
        _eventBus = eventBus;
        _logger = logger;
    }

    public Task StartAsync()
    {
        _heartbeat.OnNodeFailed += async failed =>
        {
            _logger.LogWarning("OnNodeFailed received: {NodeId}, CurrentLeader: {LeaderId}",
                failed.NodeId, _electionService.CurrentLeader?.NodeId);
            if (_electionService.CurrentLeader?.NodeId == failed.NodeId)
            {
                _logger.LogWarning("Leader failed: {NodeId}. Triggering re-election.", failed.NodeId);
                await _electionService.TryHoldElectionAsync();
            }
        };

        _eventBus.Subscribe<NodeLeftEvent>(e =>
        {
            if (_electionService.CurrentLeader?.NodeId == e.Node.NodeId)
            {
                _logger.LogWarning("Leader left via NodeLeftEvent: {NodeId}. Triggering re-election.", e.Node.NodeId);
                _ = _electionService.TryHoldElectionAsync();
            }
        });

        if (_electionService.CurrentLeader == null)
            _ = _electionService.TryHoldElectionAsync();

        return Task.CompletedTask;
    }

    public Task OnNodeJoinedAsync(NodeInfo node) => Task.CompletedTask;

    public Task OnNodeLeftAsync(NodeInfo node)
    {
        if (_electionService.CurrentLeader?.NodeId == node.NodeId)
        {
            _logger.LogWarning("Leader left: {NodeId}. Triggering re-election.", node.NodeId);
            return _electionService.TryHoldElectionAsync();
        }
        return Task.CompletedTask;
    }

    public bool ShouldRunInRole(IList<string> roles)
            => roles.Contains(ClustronRoles.Member);
}

