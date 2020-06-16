using System;
using System.Collections.Generic;

namespace MassiveJobs.Core
{
    public class DefaultTypeProvider : IJobTypeProvider
    {
        private readonly Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();
        private readonly object _lockObj = new object();
        
        public Type TagToType(string tag)
        {
            lock (_lockObj)
            {
                if (!_typeCache.TryGetValue(tag, out var type))
                {
                    type = Type.GetType(tag, false);
                    _typeCache.Add(tag, type);
                }

                return type;
            }
        }

        public string TypeToTag(Type type)
        {
            return type.FullName + ", " + type.Assembly.GetName().Name;
        }
    }
}
