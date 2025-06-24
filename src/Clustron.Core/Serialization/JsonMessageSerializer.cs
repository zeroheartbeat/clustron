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
using System.Text.Json;


namespace Clustron.Core.Serialization
{
    public class JsonMessageSerializer : IMessageSerializer
    {
        private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        public byte[] Serialize<T>(T obj) =>
            JsonSerializer.SerializeToUtf8Bytes(obj, _options);

        public byte[] Serialize(Type type, object value) =>
    JsonSerializer.SerializeToUtf8Bytes(value, type, _options);

        public T Deserialize<T>(byte[] data) =>
            JsonSerializer.Deserialize<T>(data, _options)!;

        //public T Deserialize<T>(byte[] data)
        //{
        //    var json = Encoding.UTF8.GetString(data);
        //    Console.WriteLine("[Deserialize<T>] " + json); // Debug only
        //    return JsonSerializer.Deserialize<T>(data, _options)!;
        //}

        public object Deserialize(byte[] data, Type type) =>
            JsonSerializer.Deserialize(data, type)!;
    }

}

