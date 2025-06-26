// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io
using Clustron.Core.Cluster;
using Clustron.Core.Cluster.State;
using Clustron.Core.Models;
using Microsoft.Extensions.Logging;

public abstract class ClusterNodeControllerBase : IClusterNodeController, IClusterStateMutator
{
    protected readonly NodeInfo Self;
    protected readonly JoinManager JoinManager;
    protected readonly ILogger Logger;
    protected readonly ClusterPeerManager PeerManager;
    protected readonly IClusterCommunication ClusterCommunication;
    protected readonly IClusterRuntime ClusterRuntime;

    public virtual NodeInfo? CurrentLeader => PeerManager.GetLeader();
    public virtual int CurrentEpoch => PeerManager.GetCurrentEpoch();

    public IClusterCommunication Communication => ClusterCommunication;
    public IClusterRuntime Runtime => ClusterRuntime;

    protected ClusterNodeControllerBase(
        IClusterRuntime clusterRuntime,
        IClusterCommunication clusterCommunication,
        JoinManager joinManager,
        ILogger logger)
    {
        JoinManager = joinManager;
        PeerManager = clusterRuntime.PeerManager;
        Logger = logger;
        ClusterRuntime = clusterRuntime;

        Self = clusterRuntime.Self;
        ClusterCommunication = clusterCommunication;
    }

    public abstract Task StartAsync();

    public virtual async Task<bool> IsReachableAsync()
    {
        var leader = CurrentLeader;
        if (leader == null)
            return false;

        return await ClusterCommunication.Transport.CanReachNodeAsync(leader);
    }

    public bool IsSelfLeader() => CurrentLeader?.NodeId == Self.NodeId;

    public void SetLeader(NodeInfo leader, int epoch)
    {
        Logger.LogInformation("SetLeader called for {LeaderId} with epoch {Epoch}", leader?.NodeId ?? "<null>", epoch);
        bool success = PeerManager.TrySetLeader(leader, epoch);

        if (!success)
        {
            Logger.LogDebug("Leader not updated: rejected by ClusterPeerManager.");
        }
    }

    public void ForceLeader(NodeInfo leader, int epoch)
    {
        if (leader == null || string.IsNullOrWhiteSpace(leader.NodeId))
        {
            Logger.LogWarning("ForceLeader called with invalid leader.");
            return;
        }

        Logger.LogWarning("Forcing leader to {LeaderId} at epoch {Epoch}", leader.NodeId, epoch);
        PeerManager.TrySetLeader(leader, epoch);
    }

    public async Task HandleHeartbeatSuspectAsync(string suspectedNodeId, string reporterId)
    {
        Logger.LogWarning("Node {NodeId} suspected by {ReporterId}", suspectedNodeId, reporterId);
        if (!IsSelfLeader()) return;

        if (!PeerManager.IsAlive(suspectedNodeId)) return;

        var suspect = PeerManager.GetPeerById(suspectedNodeId);
        bool reachable = await Communication.Transport.CanReachNodeAsync(suspect);

        if (!reachable)
        {
            Logger.LogInformation("Confirmed unreachable: {NodeId}, removing...", suspectedNodeId);
            await PeerManager.TryRemovePeerAsync(suspect, _ => Task.FromResult(true));
        }
    }
}
