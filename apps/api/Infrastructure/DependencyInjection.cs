using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using T4L.VideoSearch.Api.Infrastructure.Adapters;
using T4L.VideoSearch.Api.Infrastructure.Adapters.Local;
using T4L.VideoSearch.Api.Infrastructure.BackgroundJobs;
using T4L.VideoSearch.Api.Infrastructure.BackgroundJobs.Handlers;
using T4L.VideoSearch.Api.Infrastructure.BackgroundJobs.Jobs;
using T4L.VideoSearch.Api.Infrastructure.Persistence;
using T4L.VideoSearch.Api.Infrastructure.Services;
using T4L.VideoSearch.Api.Infrastructure.Services.Mock;

namespace T4L.VideoSearch.Api.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
        });

        // Blob Storage
        var blobConnectionString = configuration["BlobStorage:ConnectionString"] ?? "UseDevelopmentStorage=true";
        services.AddSingleton(new BlobServiceClient(blobConnectionString));
        services.Configure<BlobStorageOptions>(configuration.GetSection("BlobStorage"));

        // Feature flags determine which implementations to use
        var useDevAuth = configuration.GetValue<bool>("FeatureFlags:UseDevAuth");
        var useMockVideoIndexer = configuration.GetValue<bool>("FeatureFlags:UseMockVideoIndexer");
        var useMockContentSafety = configuration.GetValue<bool>("FeatureFlags:UseMockContentSafety");
        var useMockSearch = configuration.GetValue<bool>("FeatureFlags:UseMockSearch");
        var useMockTranscription = configuration.GetValue<bool>("FeatureFlags:UseMockTranscription", true);
        var useMockEncoding = configuration.GetValue<bool>("FeatureFlags:UseMockEncoding", true);

        // Register adapters based on feature flags
        // LocalChunkedBlobStore implements both IBlobStore and IChunkedBlobStore
        services.AddSingleton<LocalChunkedBlobStore>();
        services.AddSingleton<IBlobStore>(sp => sp.GetRequiredService<LocalChunkedBlobStore>());
        services.AddSingleton<IChunkedBlobStore>(sp => sp.GetRequiredService<LocalChunkedBlobStore>());

        if (useMockVideoIndexer)
        {
            services.AddSingleton<IVideoIndexerClient, MockVideoIndexerClient>();
        }
        // TODO: Add Azure implementation when needed

        if (useMockContentSafety)
        {
            services.AddSingleton<IContentSafetyClient, MockContentSafetyClient>();
        }
        // TODO: Add Azure implementation when needed

        if (useMockSearch)
        {
            // Use database-backed search for comprehensive semantic search across
            // video titles, descriptions, tags, summaries, highlights, and transcripts
            services.AddScoped<ISearchIndexClient, DatabaseSearchService>();
        }
        // TODO: Add Azure AI Search implementation when needed

        // Transcription service
        if (useMockTranscription)
        {
            services.AddSingleton<ITranscriptionService, MockTranscriptionService>();
        }
        else
        {
            services.AddSingleton<ITranscriptionService, WhisperTranscriptionService>();
        }

        // AI Highlights service
        services.AddSingleton<IAIHighlightsService, OpenAIHighlightsService>();

        // Translation service for multi-language support
        services.AddScoped<ITranslationService, OpenAITranslationService>();

        // Encoding service
        if (useMockEncoding)
        {
            services.AddSingleton<IEncodingService, MockEncodingService>();
        }
        else
        {
            services.AddSingleton<IEncodingService, FFmpegEncodingService>();
        }

        services.AddSingleton<IMalwareScanStatusProvider, MockMalwareScanStatusProvider>();
        // TODO: Add Azure Defender implementation when needed

        // Background job infrastructure
        services.AddSingleton<ChannelJobQueue>();
        services.AddSingleton<IJobQueue>(sp => sp.GetRequiredService<ChannelJobQueue>());
        services.AddHostedService<JobProcessorService>();

        // Job handlers
        services.AddScoped<IJobHandler<ProcessVideoJob>, ProcessVideoJobHandler>();
        services.AddScoped<IJobHandler<TranscribeVideoJob>, TranscribeVideoJobHandler>();
        services.AddScoped<IJobHandler<GenerateThumbnailJob>, GenerateThumbnailJobHandler>();
        services.AddScoped<IJobHandler<IndexVideoJob>, IndexVideoJobHandler>();
        services.AddScoped<IJobHandler<EncodeVideoVariantJob>, EncodeVideoVariantJobHandler>();
        services.AddScoped<IJobHandler<GenerateMasterPlaylistJob>, GenerateMasterPlaylistJobHandler>();
        services.AddScoped<IJobHandler<ContentModerationJob>, ContentModerationJobHandler>();
        services.AddScoped<IJobHandler<ExtractHighlightsJob>, ExtractHighlightsJobHandler>();

        // Health checks
        services.AddSingleton<BlobStorageHealthCheck>();

        return services;
    }
}
