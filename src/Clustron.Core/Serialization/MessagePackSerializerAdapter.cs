// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using MessagePack;
using MessagePack.Resolvers;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Clustron.Core.Serialization
{
    public class MessagePackSerializerAdapter : IMessageSerializer
    {
        private static readonly MessagePackSerializerOptions _options =
            MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance);
        
        public byte[] Serialize<T>(T obj) =>
            MessagePackSerializer.Serialize(obj, _options);

        public byte[] Serialize(Type type, object value) =>
            MessagePackSerializer.Serialize(type, value, _options);

        public T Deserialize<T>(byte[] data) =>
            MessagePackSerializer.Deserialize<T>(data, _options);

        public T Deserialize<T>(ReadOnlySpan<byte> data)
        {
            var memory = new ReadOnlyMemory<byte>(data.ToArray()); // causes allocation
            var sequence = new ReadOnlySequence<byte>(memory);
            var reader = new MessagePackReader(sequence);
            return MessagePackSerializer.Deserialize<T>(ref reader, _options);
        }

        public object Deserialize(byte[] data, Type type) =>
            MessagePackSerializer.Deserialize(type, data, _options);

    }
}

