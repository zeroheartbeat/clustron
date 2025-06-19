// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

// File: Interfaces/IClustronClient.cs
using Clustron.Client;
using Clustron.Client.Communication;
using Clustron.Client.Management;
using Clustron.Client.Models;
using Clustron.Client.Monitoring;
using Clustron.Core.Messaging;

namespace Clustron.Client;

public interface IClustronClient
{
    IClusterManager Management { get; }
    IMessenger Messaging { get; }
    IMonitor Monitoring { get; }
}

