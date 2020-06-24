﻿using System;

namespace MassiveJobs.Core.Memory
{
    public class InMemoryPublisherBuilder
    {
        private readonly MassiveJobsSettings _massiveJobsSettings;

        public static InMemoryPublisherBuilder CreateBuilder()
        {
            return new InMemoryPublisherBuilder();
        }

        private InMemoryPublisherBuilder()
        {
            _massiveJobsSettings = new MassiveJobsSettings();
            _massiveJobsSettings.MessageBrokerFactory = new InMemoryMessageBrokerFactory(_massiveJobsSettings);
        }

        public InMemoryPublisherBuilder Configure(Action<MassiveJobsSettings> configureAction)
        {
            configureAction?.Invoke(_massiveJobsSettings);
            return this;
        }

        public InMemoryPublisherBuilder UsingLoggerFactory(ILoggerFactory loggerFactory)
        {
            _massiveJobsSettings.LoggerFactory = loggerFactory;
            return this;
        }

        public InMemoryPublisherBuilder UsingServiceScopeFactory(IServiceScopeFactory serviceScopeFactory)
        {
            _massiveJobsSettings.ServiceScopeFactory = serviceScopeFactory;
            return this;
        }

        public IJobPublisher Build()
        {
            if (_massiveJobsSettings.ImmediateWorkersCount <= 0) throw new Exception($"{nameof(_massiveJobsSettings.ImmediateWorkersCount)} must be positive.");
            if (_massiveJobsSettings.ScheduledWorkersCount <= 0) throw new Exception($"{nameof(_massiveJobsSettings.ScheduledWorkersCount)} must be positive.");

            _massiveJobsSettings.JobRunner = new DefaultJobRunner(_massiveJobsSettings.LoggerFactory.SafeCreateLogger<DefaultJobRunner>());

            return new DefaultJobPublisher(_massiveJobsSettings);
        }    
    }
}
