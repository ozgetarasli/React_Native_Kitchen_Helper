using Kitchenhelper.Core.Entities;
using Kitchenhelper.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kitchenhelper.Infrastructure.Services;

public class RecipeImportProcessingService : IRecipeImportProcessingService
{
    private readonly IRecipeImportService _importService;
    private readonly IVideoAudioExtractor _audioExtractor;
    private readonly IAsrService _asrService;
    private readonly IUrlVideoService _urlVideoService;
    private readonly IRecipeDraftExtractor _draftExtractor;
    private readonly IRecipeDraftService _draftService;
    private readonly ILogger<RecipeImportProcessingService> _logger;
    private readonly string _uploadsBasePath;

    // Operational constraints
    private const long MaxFileSizeBytes = 500 * 1024 * 1024; // 500 MB
    private const int MaxDurationSeconds = 3600; // 1 hour
    private static readonly string[] AllowedMimeTypes = { "video/mp4", "video/mpeg", "video/quicktime", "video/x-msvideo" };

    public RecipeImportProcessingService(
        IRecipeImportService importService,
        IVideoAudioExtractor audioExtractor,
        IAsrService asrService,
        IUrlVideoService urlVideoService,
        IRecipeDraftExtractor draftExtractor,
        IRecipeDraftService draftService,
        ILogger<RecipeImportProcessingService> logger,
        IConfiguration configuration)
    {
        _importService = importService;
        _audioExtractor = audioExtractor;
        _asrService = asrService;
        _urlVideoService = urlVideoService;
        _draftExtractor = draftExtractor;
        _draftService = draftService;
        _logger = logger;
        _uploadsBasePath = configuration["UploadsPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
    }

    public async Task ProcessImportAsync(int importId)
    {
        _logger.LogInformation("Starting processing for import {ImportId}", importId);

        var import = await _importService.GetByIdAsync(importId);
        if (import == null)
        {
            _logger.LogWarning("Import {ImportId} not found", importId);
            return;
        }

        try
        {
            // 1. Validate source (URL için platform transcript de alınabilir)
            await ValidateSourceAsync(import);

            // 2. Extract audio
            await ExtractAudioAsync(import);

            // 3. Transcribe audio
            await TranscribeAudioAsync(import);

            // 4. Extract draft from transcript
            await ExtractDraftAsync(import);

            // 5. Mark as done
            import.Status = ImportStatus.Done;
            import.CompletedAt = DateTime.UtcNow;
            await _importService.UpdateAsync(import);

            _logger.LogInformation("Successfully processed import {ImportId}", importId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process import {ImportId}: {Error}", importId, ex.Message);
            
            import.Status = ImportStatus.Failed;
            import.ErrorMessage = GetUserFriendlyError(ex);
            import.CompletedAt = DateTime.UtcNow;
            await _importService.UpdateAsync(import);
        }
    }

    private async Task ValidateSourceAsync(RecipeImport import)
    {
        _logger.LogInformation("Validating source for import {ImportId}", import.Id);

        if (import.SourceType == SourceType.File)
        {
            // Validate file exists and is accessible
            if (string.IsNullOrEmpty(import.SourceFilePath))
                throw new InvalidOperationException("Source file path is missing");

            // Resolve relative paths
            var resolvedPath = import.SourceFilePath;
            if (!Path.IsPathRooted(resolvedPath))
            {
                // Relative path — resolve relative to current directory or uploads base
                resolvedPath = Path.Combine(Directory.GetCurrentDirectory(), import.SourceFilePath);
            }

            // Normalize path (convert forward slashes to backslashes on Windows)
            resolvedPath = Path.GetFullPath(resolvedPath);

            _logger.LogInformation("Resolved file path: {ResolvedPath}", resolvedPath);

            if (!File.Exists(resolvedPath))
                throw new FileNotFoundException("Video file not found", import.SourceFilePath);

            // Get metadata
            var (duration, fileSize) = await _audioExtractor.GetVideoMetadataAsync(resolvedPath);
            
            // Validate constraints
            if (fileSize > MaxFileSizeBytes)
                throw new InvalidOperationException($"Video file size ({fileSize / 1024 / 1024} MB) exceeds maximum allowed ({MaxFileSizeBytes / 1024 / 1024} MB)");

            if (duration > MaxDurationSeconds)
                throw new InvalidOperationException($"Video duration ({duration}s) exceeds maximum allowed ({MaxDurationSeconds}s)");

            import.FileSizeBytes = fileSize;
            import.DurationSeconds = duration;
        }
        else if (import.SourceType == SourceType.Url)
        {
            // URL validation with platform detection
            if (string.IsNullOrEmpty(import.SourceUrl))
                throw new InvalidOperationException("Source URL is missing");

            // 1. URL analizi yap
            var analysis = await _urlVideoService.AnalyzeUrlAsync(import.SourceUrl);
            
            _logger.LogInformation(
                "URL analysis for import {ImportId}: Platform={Platform}, Accessible={Accessible}, HasTranscript={HasTranscript}, Downloadable={Downloadable}, HasRecipeInDesc={HasRecipeInDesc}",
                import.Id, analysis.Platform, analysis.IsAccessible, analysis.HasTranscriptApi, analysis.IsDownloadable, analysis.HasRecipeInDescription);

            // 2. Erişilebilirlik kontrolü
            if (!analysis.IsAccessible)
            {
                var errorMsg = analysis.ErrorMessage ?? "Video URL'sine erişilemiyor";
                var userAction = analysis.UserAction ?? "Lütfen videoyu dosya olarak yükleyin";
                throw new InvalidOperationException($"{errorMsg}. {userAction}");
            }

            // 3. İndirilebilirlik kontrolü (erken çık)
            if (!analysis.IsDownloadable)
            {
                var errorMsg = analysis.FailureReason ?? "Bu URL'den video indirilemedi";
                var userAction = analysis.UserAction ?? "Lütfen videoyu dosya olarak yükleyin";
                throw new InvalidOperationException($"{errorMsg}. {userAction}");
            }

            var importDir = Path.Combine(_uploadsBasePath, "imports", import.Id.ToString());
            Directory.CreateDirectory(importDir);
            
            // ═══════════════════════════════════════════════════════════════════
            // PARALLEL DOWNLOAD: Altyazı + Ses aynı anda indirilecek!
            // ═══════════════════════════════════════════════════════════════════
            _logger.LogInformation("⚡ Starting PARALLEL download: Audio + Subtitles simultaneously...");

            var contextBuilder = new System.Text.StringBuilder();
            
            // Description zaten analysis'te var, hemen ekle
            if (!string.IsNullOrWhiteSpace(analysis.VideoDescription))
            {
                _logger.LogInformation("Description found (length: {Length} chars).", analysis.VideoDescription.Length);
                contextBuilder.AppendLine($"=== VİDEO AÇIKLAMASI (KAYNAK: {analysis.Platform}) ===");
                contextBuilder.AppendLine(analysis.VideoDescription);
                contextBuilder.AppendLine();
            }

            // Paralel görevleri tanımla
            var audioPath = Path.Combine(importDir, "audio.wav");
            string? platformTranscript = null;
            bool audioDownloaded = false;

            // Task 1: Altyazı indirme (varsa)
            var subtitleTask = Task.Run(async () =>
            {
                if (analysis.HasTranscriptApi)
                {
                    _logger.LogInformation("📥 [PARALLEL] Fetching platform subtitles...");
                    platformTranscript = await _urlVideoService.TryGetPlatformTranscriptAsync(import.SourceUrl);
                    if (!string.IsNullOrWhiteSpace(platformTranscript))
                    {
                        _logger.LogInformation("✅ [PARALLEL] Subtitles retrieved ({Length} chars)", platformTranscript.Length);
                    }
                }
            });

            // Task 2: Ses indirme
            var audioTask = Task.Run(async () =>
            {
                _logger.LogInformation("📥 [PARALLEL] Downloading audio only to {Path}...", audioPath);
                audioDownloaded = await _urlVideoService.DownloadAudioOnlyAsync(import.SourceUrl, audioPath);
                if (audioDownloaded && File.Exists(audioPath))
                {
                    _logger.LogInformation("✅ [PARALLEL] Audio downloaded! Size: {Size} KB", 
                        new FileInfo(audioPath).Length / 1024);
                }
            });

            // Her iki görevi de bekle (paralel)
            await Task.WhenAll(subtitleTask, audioTask);
            
            _logger.LogInformation("⚡ PARALLEL download complete!");

            // Altyazıları context'e ekle
            if (!string.IsNullOrWhiteSpace(platformTranscript))
            {
                contextBuilder.AppendLine("=== VİDEO ALT YAZILARI ===");
                contextBuilder.AppendLine(platformTranscript);
                contextBuilder.AppendLine();
            }

            // Context'i kaydet
            if (contextBuilder.Length > 0)
            {
                var contextPath = Path.Combine(importDir, "context.txt");
                await File.WriteAllTextAsync(contextPath, contextBuilder.ToString());
            }

            // Ses indirme sonucunu işle
            if (!audioDownloaded || !File.Exists(audioPath))
            {
                _logger.LogWarning("Audio-only download failed. Falling back to full video download...");
                
                // Fallback: Video indir ve sesi çıkar
                var videoPath = Path.Combine(importDir, "source.mp4");
                var videoDownloaded = await _urlVideoService.DownloadVideoAsync(import.SourceUrl, videoPath);
                
                if (!videoDownloaded)
                {
                    throw new InvalidOperationException("Video indirilemedi. Lütfen videoyu dosya olarak yükleyin");
                }
                
                import.SourceFilePath = videoPath;
                if (File.Exists(videoPath))
                {
                    import.FileSizeBytes = new FileInfo(videoPath).Length;
                }
            }
            else
            {
                // Ses başarıyla indirildi - AudioPath'i ayarla
                import.AudioPath = audioPath;
            }

            import.DurationSeconds = analysis.DurationSeconds;
        }

        await _importService.UpdateAsync(import);
    }

    private async Task ExtractAudioAsync(RecipeImport import)
    {
        _logger.LogInformation("Extracting/verifying audio for import {ImportId}", import.Id);

        // URL sources with audio-only download already have the audio file
        if (!string.IsNullOrEmpty(import.AudioPath) && File.Exists(import.AudioPath))
        {
            _logger.LogInformation("✅ Audio already available at {AudioPath}, skipping extraction", import.AudioPath);
            
            // Just verify duration if not set
            if (!import.DurationSeconds.HasValue || import.DurationSeconds == 0)
            {
                try
                {
                    var (audioDuration, _) = await _audioExtractor.GetVideoMetadataAsync(import.AudioPath);
                    import.DurationSeconds = audioDuration;
                    await _importService.UpdateAsync(import);
                }
                catch { /* ignore */ }
            }
            return;
        }

        // File-based imports or fallback: extract audio from video
        string? videoPath = import.SourceFilePath;
        
        if (string.IsNullOrEmpty(videoPath))
        {
            throw new InvalidOperationException("Video dosyası bulunamadı. Lütfen dosya olarak yükleyin.");
        }
        
        // Resolve relative paths
        if (!Path.IsPathRooted(videoPath))
        {
            videoPath = Path.Combine(Directory.GetCurrentDirectory(), videoPath);
        }

        // Normalize path (convert forward slashes to backslashes on Windows)
        videoPath = Path.GetFullPath(videoPath);
        
        if (!File.Exists(videoPath))
        {
            throw new FileNotFoundException("Video dosyası bulunamadı", videoPath);
        }

        // Create output directory
        var importDir = Path.Combine(_uploadsBasePath, "imports", import.Id.ToString());
        Directory.CreateDirectory(importDir);

        // Set output path: uploads/imports/{importId}/audio.wav
        var audioPath = Path.Combine(importDir, "audio.wav");

        // Extract audio (WAV, 16kHz, mono)
        var duration = await _audioExtractor.ExtractAudioAsync(videoPath, audioPath);
        
        import.AudioPath = audioPath;
        import.DurationSeconds = duration;
        await _importService.UpdateAsync(import);

        _logger.LogInformation("Audio extracted to {AudioPath} (duration: {Duration}s)", audioPath, duration);
    }

    private async Task TranscribeAudioAsync(RecipeImport import)
    {
        _logger.LogInformation("Transcribing audio for import {ImportId}", import.Id);

        // Check if we have context (subtitles + description) that's sufficient
        var importDir = import.AudioPath != null 
            ? Path.GetDirectoryName(import.AudioPath)!
            : Path.Combine(_uploadsBasePath, "imports", import.Id.ToString());
        var contextPath = Path.Combine(importDir, "context.txt");
        var hasContext = File.Exists(contextPath);
        
        string? contextContent = null;
        if (hasContext)
        {
            contextContent = await File.ReadAllTextAsync(contextPath);
        }

        // OPTIMIZATION: If we have substantial context (subtitles/description), skip Whisper!
        // Minimum 500 chars of context is considered sufficient for recipe extraction
        const int MinContextLengthToSkipWhisper = 500;
        var hasSufficientContext = !string.IsNullOrWhiteSpace(contextContent) 
            && contextContent.Length >= MinContextLengthToSkipWhisper;

        if (hasSufficientContext)
        {
            _logger.LogInformation(
                "⚡ FAST MODE: Skipping Whisper ASR! Using platform subtitles/description ({Length} chars)", 
                contextContent!.Length);
            
            // Use context directly as transcript
            var transcriptPath = Path.Combine(importDir, "transcript.txt");
            await File.WriteAllTextAsync(transcriptPath, contextContent);
            
            import.TranscriptPath = transcriptPath;
            import.TranscriptSource = TranscriptSource.PlatformSubtitles;
            await _importService.UpdateAsync(import);
            
            _logger.LogInformation(
                "✅ Transcript saved (platform subtitles only) - Whisper SKIPPED! Saved ~60-90 seconds");
            return;
        }

        // No sufficient context - must use Whisper ASR
        _logger.LogInformation("No sufficient platform content, running Whisper ASR...");

        if (string.IsNullOrEmpty(import.AudioPath) || !File.Exists(import.AudioPath))
            throw new InvalidOperationException("Audio file not found");

        // Transcribe using ASR service
        var transcript = await _asrService.TranscribeAsync(import.AudioPath);

        if (string.IsNullOrWhiteSpace(transcript))
            throw new InvalidOperationException("Transcription resulted in empty text");

        var combined = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(contextContent))
        {
            combined.AppendLine("=== BAĞLAM ===");
            combined.AppendLine(contextContent.Trim());
            combined.AppendLine();
        }

        combined.AppendLine("=== WHISPER ASR ===");
        combined.AppendLine(transcript);

        // Save transcript to file: uploads/imports/{importId}/transcript.txt
        var finalTranscriptPath = Path.Combine(importDir, "transcript.txt");

        await File.WriteAllTextAsync(finalTranscriptPath, combined.ToString());

        import.TranscriptPath = finalTranscriptPath;
        import.TranscriptSource = !string.IsNullOrWhiteSpace(contextContent) 
            ? TranscriptSource.Combined 
            : TranscriptSource.WhisperASR;
        await _importService.UpdateAsync(import);

        _logger.LogInformation("Transcript saved to {TranscriptPath} ({Length} chars) using Whisper ASR", 
            finalTranscriptPath, combined.Length);
    }

    private async Task ExtractDraftAsync(RecipeImport import)
    {
        _logger.LogInformation("Extracting draft for import {ImportId}", import.Id);

        // Read transcript
        if (string.IsNullOrEmpty(import.TranscriptPath) || !File.Exists(import.TranscriptPath))
        {
            throw new InvalidOperationException("Transcript dosyası bulunamadı");
        }

        var transcriptText = await File.ReadAllTextAsync(import.TranscriptPath);

        if (string.IsNullOrWhiteSpace(transcriptText))
        {
            throw new InvalidOperationException("Transcript boş");
        }

        _logger.LogInformation("Loaded transcript for draft extraction: {Length} chars", transcriptText.Length);

        try
        {
            // Extract draft using LLM with the specified recipe language
            var draftJson = await _draftExtractor.ExtractDraftAsync(transcriptText, import.RecipeLanguage);

            // Save draft to database
            var draft = await _draftService.CreateAsync(import.UserId, import.Id, draftJson);

            // Also save draft JSON to file for backup
            var draftPath = Path.Combine(
                Path.GetDirectoryName(import.TranscriptPath)!,
                "draft.json");
            await File.WriteAllTextAsync(draftPath, draftJson);

            _logger.LogInformation("Draft extracted and saved for import {ImportId}, draft {DraftId}", 
                import.Id, draft.Id);
        }
        catch (DraftExtractionException ex)
        {
            _logger.LogError(ex, "Draft extraction failed for import {ImportId}: {Error}", 
                import.Id, ex.Message);
            
            // Don't fail the import, just log the error
            // The import is still "Done" but draft extraction failed
            // User can retry draft extraction later or edit manually
            throw new InvalidOperationException($"Tarif çıkarma başarısız: {ex.Message}");
        }
    }

    private string GetUserFriendlyError(Exception ex)
    {
        return ex switch
        {
            FileNotFoundException => "Video dosyası bulunamadı",
            DraftExtractionException dex => $"Tarif çıkarılamadı: {dex.Message}",
            InvalidOperationException => ex.Message,
            NotImplementedException => "Bu özellik henüz desteklenmiyor",
            HttpRequestException => "Video URL'sine erişilemiyor",
            _ => "İşlem sırasında bir hata oluştu. Lütfen tekrar deneyin."
        };
    }
}
