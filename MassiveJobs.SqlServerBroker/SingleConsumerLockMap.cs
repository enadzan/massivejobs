using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MassiveJobs.SqlServerBroker
{
    public class SingleConsumerLockMap : IEntityTypeConfiguration<SingleConsumerLock>
    {
        public void Configure(EntityTypeBuilder<SingleConsumerLock> builder)
        {
            builder.ToTable("single_consumer_lock", "massive_jobs");

            builder.HasKey(e => e.RoutingKey)
                .HasName("pk__single_consumer_lock");

            builder.Property(e => e.RoutingKey)
                .HasColumnName("routing_key")
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(e => e.InstanceName)
                .HasColumnName("instance_name")
                .HasMaxLength(256);

            builder.Property(e => e.LockKeepAliveUtc)
                .HasColumnName("lock_keep_alive_utc");
        }
    }
}
