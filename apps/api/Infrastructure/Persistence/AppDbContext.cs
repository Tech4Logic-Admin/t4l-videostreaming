using Microsoft.EntityFrameworkCore;
using T4L.VideoSearch.Api.Domain.Entities;

namespace T4L.VideoSearch.Api.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<VideoAsset> VideoAssets => Set<VideoAsset>();
    public DbSet<VideoProcessingJob> VideoProcessingJobs => Set<VideoProcessingJob>();
    public DbSet<VideoVariant> VideoVariants => Set<VideoVariant>();
    public DbSet<VideoHighlight> VideoHighlights => Set<VideoHighlight>();
    public DbSet<HighlightTranslation> HighlightTranslations => Set<HighlightTranslation>();
    public DbSet<VideoSummary> VideoSummaries => Set<VideoSummary>();
    public DbSet<ModerationResult> ModerationResults => Set<ModerationResult>();
    public DbSet<TranscriptSegment> TranscriptSegments => Set<TranscriptSegment>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SearchQueryLog> SearchQueryLogs => Set<SearchQueryLog>();
    public DbSet<DailyMetrics> DailyMetrics => Set<DailyMetrics>();
    public DbSet<UploadSession> UploadSessions => Set<UploadSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // VideoAsset configuration
        modelBuilder.Entity<VideoAsset>(entity =>
        {
            entity.ToTable("video_assets");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(5000);
            entity.Property(e => e.Tags).HasColumnName("tags").HasColumnType("text[]");
            entity.Property(e => e.LanguageHint).HasColumnName("language_hint").HasMaxLength(10);
            entity.Property(e => e.DurationMs).HasColumnName("duration_ms");
            entity.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.BlobPath).HasColumnName("blob_path").HasMaxLength(1000).IsRequired();
            entity.Property(e => e.ThumbnailPath).HasColumnName("thumbnail_path").HasMaxLength(1000);
            entity.Property(e => e.MasterPlaylistPath).HasColumnName("master_playlist_path").HasMaxLength(1000);
            entity.Property(e => e.CreatedByOid).HasColumnName("created_by_oid").HasMaxLength(100).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.AllowedGroupIds).HasColumnName("allowed_group_ids").HasColumnType("text[]");
            entity.Property(e => e.AllowedUserOids).HasColumnName("allowed_user_oids").HasColumnType("text[]");

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedByOid);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.CreatedAt);
        });

        // VideoProcessingJob configuration
        modelBuilder.Entity<VideoProcessingJob>(entity =>
        {
            entity.ToTable("video_processing_jobs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.VideoId).HasColumnName("video_id");
            entity.Property(e => e.Stage).HasColumnName("stage").HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Attempts).HasColumnName("attempts");
            entity.Property(e => e.Progress).HasColumnName("progress").HasDefaultValue(0);
            entity.Property(e => e.ProgressMessage).HasColumnName("progress_message").HasMaxLength(500);
            entity.Property(e => e.LastError).HasColumnName("last_error").HasMaxLength(5000);
            entity.Property(e => e.ExternalJobId).HasColumnName("external_job_id").HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Video)
                .WithMany(v => v.ProcessingJobs)
                .HasForeignKey(e => e.VideoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.VideoId, e.Stage });
            entity.HasIndex(e => e.Status);
        });

        // ModerationResult configuration
        modelBuilder.Entity<ModerationResult>(entity =>
        {
            entity.ToTable("moderation_results");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.VideoId).HasColumnName("video_id");
            entity.Property(e => e.Reasons).HasColumnName("reasons").HasColumnType("text[]");
            entity.Property(e => e.MalwareScanStatus).HasColumnName("malware_scan_status").HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.ContentSafetyStatus).HasColumnName("content_safety_status").HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.HighestSeverity).HasColumnName("highest_severity").HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.ReviewerDecision).HasColumnName("reviewer_decision").HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.ReviewerOid).HasColumnName("reviewer_oid").HasMaxLength(100);
            entity.Property(e => e.ReviewerNotes).HasColumnName("reviewer_notes").HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");

            entity.HasOne(e => e.Video)
                .WithOne(v => v.ModerationResult)
                .HasForeignKey<ModerationResult>(e => e.VideoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.VideoId).IsUnique();
        });

        // TranscriptSegment configuration
        modelBuilder.Entity<TranscriptSegment>(entity =>
        {
            entity.ToTable("transcript_segments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.VideoId).HasColumnName("video_id");
            entity.Property(e => e.StartMs).HasColumnName("start_ms");
            entity.Property(e => e.EndMs).HasColumnName("end_ms");
            entity.Property(e => e.Text).HasColumnName("text").IsRequired();
            entity.Property(e => e.DetectedLanguage).HasColumnName("detected_language").HasMaxLength(10);
            entity.Property(e => e.Speaker).HasColumnName("speaker").HasMaxLength(200);
            entity.Property(e => e.EmbeddingVector).HasColumnName("embedding_vector");
            entity.Property(e => e.Confidence).HasColumnName("confidence");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.Video)
                .WithMany(v => v.TranscriptSegments)
                .HasForeignKey(e => e.VideoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.VideoId);
            entity.HasIndex(e => new { e.VideoId, e.StartMs });
        });

        // VideoVariant configuration
        modelBuilder.Entity<VideoVariant>(entity =>
        {
            entity.ToTable("video_variants");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.VideoId).HasColumnName("video_id");
            entity.Property(e => e.Quality).HasColumnName("quality").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Width).HasColumnName("width");
            entity.Property(e => e.Height).HasColumnName("height");
            entity.Property(e => e.VideoBitrateKbps).HasColumnName("video_bitrate_kbps");
            entity.Property(e => e.AudioBitrateKbps).HasColumnName("audio_bitrate_kbps");
            entity.Property(e => e.PlaylistPath).HasColumnName("playlist_path").HasMaxLength(1000);
            entity.Property(e => e.SegmentsPath).HasColumnName("segments_path").HasMaxLength(1000);
            entity.Property(e => e.FileSizeBytes).HasColumnName("file_size_bytes");
            entity.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Progress).HasColumnName("progress").HasDefaultValue(0);
            entity.Property(e => e.ProgressMessage).HasColumnName("progress_message").HasMaxLength(500);
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");

            entity.HasOne(e => e.Video)
                .WithMany(v => v.Variants)
                .HasForeignKey(e => e.VideoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.VideoId);
            entity.HasIndex(e => new { e.VideoId, e.Quality }).IsUnique();
        });

        // VideoHighlight configuration
        modelBuilder.Entity<VideoHighlight>(entity =>
        {
            entity.ToTable("video_highlights");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.VideoId).HasColumnName("video_id");
            entity.Property(e => e.Text).HasColumnName("text").IsRequired();
            entity.Property(e => e.OriginalText).HasColumnName("original_text");
            entity.Property(e => e.SourceLanguage).HasColumnName("source_language").HasMaxLength(10);
            entity.Property(e => e.Category).HasColumnName("category").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Importance).HasColumnName("importance");
            entity.Property(e => e.TimestampMs).HasColumnName("timestamp_ms");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.Video)
                .WithMany(v => v.Highlights)
                .HasForeignKey(e => e.VideoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.VideoId);
            entity.HasIndex(e => e.Category);
        });

        // HighlightTranslation configuration
        modelBuilder.Entity<HighlightTranslation>(entity =>
        {
            entity.ToTable("highlight_translations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.VideoHighlightId).HasColumnName("video_highlight_id");
            entity.Property(e => e.LanguageCode).HasColumnName("language_code").HasMaxLength(10).IsRequired();
            entity.Property(e => e.TranslatedText).HasColumnName("translated_text").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.Highlight)
                .WithMany(h => h.Translations)
                .HasForeignKey(e => e.VideoHighlightId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.VideoHighlightId);
            entity.HasIndex(e => new { e.VideoHighlightId, e.LanguageCode }).IsUnique();
        });

        // VideoSummary configuration
        modelBuilder.Entity<VideoSummary>(entity =>
        {
            entity.ToTable("video_summaries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.VideoId).HasColumnName("video_id");
            entity.Property(e => e.Summary).HasColumnName("summary");
            entity.Property(e => e.TlDr).HasColumnName("tldr").HasMaxLength(500);
            entity.Property(e => e.OriginalSummary).HasColumnName("original_summary");
            entity.Property(e => e.OriginalTlDr).HasColumnName("original_tldr").HasMaxLength(500);
            entity.Property(e => e.SourceLanguage).HasColumnName("source_language").HasMaxLength(10);
            entity.Property(e => e.Keywords).HasColumnName("keywords").HasColumnType("text[]");
            entity.Property(e => e.Topics).HasColumnName("topics").HasColumnType("text[]");
            entity.Property(e => e.Sentiment).HasColumnName("sentiment").HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Video)
                .WithOne(v => v.Summary)
                .HasForeignKey<VideoSummary>(e => e.VideoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.VideoId).IsUnique();
        });

        // UserProfile configuration
        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.ToTable("user_profiles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Oid).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(500).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(500).IsRequired();
            entity.Property(e => e.GroupIds).HasColumnType("text[]");
            entity.Property(e => e.Role).HasConversion<string>().HasMaxLength(50);

            entity.HasIndex(e => e.Oid).IsUnique();
            entity.HasIndex(e => e.Email);
            entity.HasIndex(e => e.TenantId);
        });

        // AuditLog configuration
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.ActorOid).HasColumnName("actor_oid").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
            entity.Property(e => e.TargetType).HasColumnName("target_type").HasMaxLength(100).IsRequired();
            entity.Property(e => e.TargetId).HasColumnName("target_id");
            entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasColumnName("user_agent").HasMaxLength(500);
            entity.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(e => e.ActorOid);
            entity.HasIndex(e => e.Action);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.TenantId);
        });

        // SearchQueryLog configuration
        modelBuilder.Entity<SearchQueryLog>(entity =>
        {
            entity.ToTable("search_query_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ActorOid).HasColumnName("actor_oid").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Query).HasColumnName("query").HasMaxLength(1000).IsRequired();
            entity.Property(e => e.Language).HasColumnName("language").HasMaxLength(10);
            entity.Property(e => e.ResultsCount).HasColumnName("results_count");
            entity.Property(e => e.LatencyMs).HasColumnName("latency_ms");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");

            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.TenantId);
        });

        // DailyMetrics configuration
        modelBuilder.Entity<DailyMetrics>(entity =>
        {
            entity.ToTable("daily_metrics");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => new { e.TenantId, e.Date }).IsUnique();
        });

        // UploadSession configuration
        modelBuilder.Entity<UploadSession>(entity =>
        {
            entity.ToTable("upload_sessions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.FileName).HasColumnName("file_name").HasMaxLength(500).IsRequired();
            entity.Property(e => e.ContentType).HasColumnName("content_type").HasMaxLength(100).IsRequired();
            entity.Property(e => e.FileSize).HasColumnName("file_size");
            entity.Property(e => e.ChunkSize).HasColumnName("chunk_size");
            entity.Property(e => e.TotalChunks).HasColumnName("total_chunks");
            entity.Property(e => e.UploadedChunks).HasColumnName("uploaded_chunks");
            entity.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.BlobPath).HasColumnName("blob_path").HasMaxLength(1000).IsRequired();
            entity.Property(e => e.CreatedByOid).HasColumnName("created_by_oid").HasMaxLength(100).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(5000);
            entity.Property(e => e.Tags).HasColumnName("tags").HasColumnType("text[]");
            entity.Property(e => e.LanguageHint).HasColumnName("language_hint").HasMaxLength(10);
            entity.Property(e => e.BlockIds).HasColumnName("block_ids").HasColumnType("text[]");
            entity.Property(e => e.VideoAssetId).HasColumnName("video_asset_id");

            entity.HasOne(e => e.VideoAsset)
                .WithMany()
                .HasForeignKey(e => e.VideoAssetId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.CreatedByOid);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ExpiresAt);
        });
    }
}
