using System;
using System.Collections.Generic;

namespace MassiveJobs.Core.Serialization
{
    public class DefaultTypeProvider : IJobTypeProvider
    {
        private readonly Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();
        private readonly object _lockObj = new object();
        
        public Type TagToType(string tag)
        {
            lock (_lockObj)
            {
                if (_typeCache.TryGetValue(tag, out var type)) return type;

                type = Type.GetType(tag, false) ?? throw new Exception("Unknown type: " + tag);

                _typeCache.Add(tag, type);

                return type;
            }
        }

        public string TypeToTag(Type type)
        {
            return type.FullName + ", " + type.Assembly.GetName().Name;
        }
    }
}
