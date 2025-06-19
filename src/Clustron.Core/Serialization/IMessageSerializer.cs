// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

namespace Clustron.Core.Serialization;
public interface IMessageSerializer
{
    byte[] Serialize<T>(T obj);
    T Deserialize<T>(byte[] data);

    object Deserialize(byte[] data, Type type);
}
