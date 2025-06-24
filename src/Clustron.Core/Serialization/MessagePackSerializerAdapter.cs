// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack;


namespace Clustron.Core.Serialization
{
    public class MessagePackSerializerAdapter : IMessageSerializer
    {
        public byte[] Serialize<T>(T obj) => MessagePackSerializer.Serialize(obj);
        public T Deserialize<T>(byte[] data) => MessagePackSerializer.Deserialize<T>(data);

        public object Deserialize(byte[] data, Type type) =>
            MessagePackSerializer.Deserialize(type, data);

        public byte[] Serialize(Type type, object value)
        {
            throw new NotImplementedException();
        }
    }
}

