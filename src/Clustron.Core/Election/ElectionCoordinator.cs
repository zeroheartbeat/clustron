// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Core.Election;
using Clustron.Core.Models;
using Microsoft.Extensions.Logging;

namespace Clustron.Core.Election;

public class ElectionCoordinator
{
    private readonly NodeInfo _self;
    private readonly IElectionStrategy _electionStrategy;
    private readonly ILogger<ElectionCoordinator> _logger;

    private bool _electionInProgress = false;
    private DateTime _lastElectionTime = DateTime.MinValue;
    private readonly TimeSpan _electionCooldown = TimeSpan.FromSeconds(3);

    public ElectionCoordinator(
        NodeInfo self,
        IElectionStrategy electionStrategy,
        ILogger<ElectionCoordinator> logger)
    {
        _self = self;
        _electionStrategy = electionStrategy;
        _logger = logger;
    }

    public async Task<NodeInfo?> ElectLeaderAsync(IEnumerable<NodeInfo> activePeers)
    {
        if (_electionInProgress || DateTime.UtcNow - _lastElectionTime < _electionCooldown)
        {
            _logger.LogWarning("Election skipped: already in progress or cooldown.");
            return null;
        }

        _electionInProgress = true;
        _lastElectionTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Running election among: {Peers}", string.Join(", ", activePeers.Select(p => p.NodeId)));
            var elected = await _electionStrategy.ElectLeaderAsync(activePeers, _self);

            if (elected == null)
            {
                _logger.LogWarning("No eligible leader could be elected.");
            }

            return elected;
        }
        finally
        {
            _electionInProgress = false;
        }
    }
}

