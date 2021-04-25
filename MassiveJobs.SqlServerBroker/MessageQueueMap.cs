using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MassiveJobs.SqlServerBroker
{
    public class MessageQueueMap : IEntityTypeConfiguration<MessageQueue>
    {
        public void Configure(EntityTypeBuilder<MessageQueue> builder)
        {
            builder.ToTable("message_queue", "massive_jobs");

            builder.HasKey(e => new { e.RoutingKey, e.Id })
                .HasName("pk__message_queue");

            builder.Property(e => e.RoutingKey)
                .HasColumnName("routing_key")
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            builder.Property(e => e.MessageType)
                .HasColumnName("message_type")
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(e => e.MessageData)
                .HasColumnName("message_data")
                .HasColumnType("nvarchar(max)");

            builder.Property(e => e.PublishedAtUtc)
                .HasColumnName("published_at_utc");

            builder.Property(e => e.ProcessingInstance)
                .HasColumnName("processing_instance")
                .HasMaxLength(256);

            builder.Property(e => e.ProcessingStartUtc)
                .HasColumnName("processing_start_utc");

            builder.Property(e => e.ProcessingKeepAliveUtc)
                .HasColumnName("processing_keepalive_utc");

            builder.Property(e => e.ProcessingEndUtc)
                .HasColumnName("processing_end_utc");

            builder.HasIndex(e => new { e.RoutingKey, e.ProcessingEndUtc, e.ProcessingKeepAliveUtc })
                .HasFilter("processing_end_utc is null")
                .HasDatabaseName("ix__message_queue__routing_key__processing_end_utc");
        }
    }
}
