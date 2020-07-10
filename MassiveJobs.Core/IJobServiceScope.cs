﻿using System;

namespace MassiveJobs.Core
{
    public interface IJobServiceScope: IDisposable
    {
        object GetService(Type type);
        object GetRequiredService(Type type);
    }

    public interface IJobServiceScopeFactory: IDisposable
    {
        IJobServiceScope CreateScope();
    }
}