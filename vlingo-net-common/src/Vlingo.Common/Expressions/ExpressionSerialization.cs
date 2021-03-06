// Copyright © 2012-2020 VLINGO LABS. All rights reserved.
//
// This Source Code Form is subject to the terms of the
// Mozilla Public License, v. 2.0. If a copy of the MPL
// was not distributed with this file, You can obtain
// one at https://mozilla.org/MPL/2.0/.

using System;
using System.Linq;
using System.Linq.Expressions;
using Newtonsoft.Json.Linq;
using Vlingo.Common.Serialization;

namespace Vlingo.Common.Expressions
{
    public static class ExpressionSerialization
    {
        public static string Serialize<TProtocol>(Expression<Action<TProtocol>> expression)
        {
            var mce = (MethodCallExpression)expression.Body;
            
            var interfaceName = typeof(TProtocol).Name;
            var methodName = mce.Method.Name;

            var args = mce.Arguments.Select(ExpressionExtensions.Evaluate).ToArray();
            var types = args.Select(a => a.GetType()).ToArray();
            return JsonSerialization.Serialized(new ExpressionSerializationInfo(interfaceName, methodName, args, types));
        }

        public static ExpressionSerializationInfo Deserialize<TProtocol>(string serialized)
        {
            var deserialized = JsonSerialization.Deserialized<ExpressionSerializationInfo>(serialized);
            var i = 0;
            foreach (var arg in deserialized.Args)
            {
                if (arg is JObject jobject)
                {
                    deserialized.Args[i] = jobject.ToObject(deserialized.Types[i])!;
                }
                // special case as underlying serializer converts ints to longs en deserialization
                else if (arg is long && arg.GetType() != deserialized.Types[i])
                {
                    deserialized.Args[i] = int.Parse(arg.ToString());
                }

                i++;
            }

            return deserialized;
        }
    }
}