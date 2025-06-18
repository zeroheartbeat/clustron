// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Core.Cluster;
using Clustron.Core.Configuration;
using Clustron.Core.Discovery;
using Clustron.Core.Serialization;
using Clustron.Core.Transport;
using Microsoft.Extensions.Options;

namespace Clustron.Core.Transport;

public class TransportFactory : ITransportFactory
{
    private readonly ClustronConfig _config;
    private readonly IClusterRuntime _clusterRuntime;
    private readonly IMessageSerializer _serializer;
    private readonly IDiscoveryProvider _discovery;
    private readonly IClusterLoggerProvider _loggerProvider;
    private readonly IClusterCommunication _communication;

    public TransportFactory(
        IOptions<ClustronConfig> config,
        IClusterRuntime clusterRuntime,
        IClusterCommunication communication,
        IMessageSerializer serializer,
        IDiscoveryProvider discovery,
        IClusterLoggerProvider loggerProvider)
    {
        _config = config.Value;
        _clusterRuntime = clusterRuntime;
        _serializer = serializer;
        _discovery = discovery;
        _communication = communication;
        _loggerProvider = loggerProvider;
    }

    public ITransport Create()
    {
        ITransport transport = _config.UseDuplexConnections
            ? new DuplexTcpTransport(_config.Port, _clusterRuntime, _serializer, _loggerProvider, _config.RetryOptions)
            : new UnidirectionalTcpTransport(_config.Port, _clusterRuntime, _serializer, _loggerProvider);

        _communication.OverrideTransport(transport); // important for ClusterContext transport access
        return transport;
    }
}

