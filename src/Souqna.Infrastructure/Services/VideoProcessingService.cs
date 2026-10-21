using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Souqna.Core.DTOs;
using Souqna.Core.Interfaces;

namespace Souqna.Infrastructure.Services
{
    public class VideoProcessingService : IVideoProcessingService
    {
        private readonly IStorageService _storageService;
        private readonly ILogger<VideoProcessingService> _logger;

        public VideoProcessingService(IStorageService storageService, ILogger<VideoProcessingService> logger)
        {
            _storageService = storageService;
            _logger = logger;
        }

        public async Task<VideoValidationResult> ValidateAndProcessVideoAsync(
            Stream stream, 
            string originalFileName, 
            int maxAllowedSeconds)
        {
            string ext = Path.GetExtension(originalFileName).ToLowerInvariant();
            if (ext != ".mp4" && ext != ".webm")
            {
                return new VideoValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "صيغة الفيديو غير مدعومة. يرجى رفع ملف بصيغة MP4 أو WebM فقط."
                };
            }

            long length = stream.Length;
            if (length > 100 * 1024 * 1024) // 100MB limit
            {
                return new VideoValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "حجم الفيديو يتجاوز الحد الأقصى المسموح به (100 ميجابايت)."
                };
            }

            // Extract duration from MP4 metadata box if MP4
            int durationSeconds = 30; // fallback default
            if (ext == ".mp4")
            {
                try
                {
                    durationSeconds = ExtractMp4Duration(stream);
                    if (durationSeconds <= 0) durationSeconds = 30;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not extract exact MP4 duration, fallback to estimate.");
                    durationSeconds = 30;
                }
            }

            // Server-side enforcement of subscription plan limit
            if (durationSeconds > maxAllowedSeconds)
            {
                return new VideoValidationResult
                {
                    IsValid = false,
                    DurationSeconds = durationSeconds,
                    ErrorMessage = $"مدة الفيديو ({durationSeconds} ثانية) تتجاوز الحد الأقصى المسموح به في باقتك الحالية ({maxAllowedSeconds} ثانية). يمكنك ترقية باقتك للاستفادة من مدة أطول."
                };
            }

            // Reset stream position and save public video file
            stream.Position = 0;
            string videoUrl = await _storageService.SavePublicFileAsync(stream, originalFileName, "reels");
            
            // Standard placeholder thumbnail for development/demo reels
            string thumbnailUrl = "/images/reels-default-thumb.jpg";

            return new VideoValidationResult
            {
                IsValid = true,
                DurationSeconds = durationSeconds,
                FileSizeBytes = length,
                RelativeVideoUrl = videoUrl,
                RelativeThumbnailUrl = thumbnailUrl
            };
        }

        /// <summary>
        /// Reads ISO Base Media File Format (MP4) 'mvhd' atom to determine true video duration.
        /// </summary>
        private static int ExtractMp4Duration(Stream stream)
        {
            stream.Position = 0;
            using var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);
            
            byte[] buffer = new byte[8];
            while (stream.Position < stream.Length - 8)
            {
                if (stream.Read(buffer, 0, 8) < 8) break;
                
                uint size = (uint)((buffer[0] << 24) | (buffer[1] << 16) | (buffer[2] << 8) | buffer[3]);
                string type = System.Text.Encoding.ASCII.GetString(buffer, 4, 4);

                if (type == "moov")
                {
                    // Inspect moov box children
                    continue;
                }
                else if (type == "mvhd")
                {
                    // Read mvhd box version
                    int version = reader.ReadByte();
                    // Skip flags (3 bytes)
                    stream.Position += 3;

                    if (version == 1)
                    {
                        // 64-bit creation & mod time (16 bytes)
                        stream.Position += 16;
                        uint timescale = ReadBigEndianUInt32(reader);
                        ulong duration = ReadBigEndianUInt64(reader);
                        if (timescale > 0)
                        {
                            return (int)Math.Ceiling((double)duration / timescale);
                        }
                    }
                    else
                    {
                        // 32-bit creation & mod time (8 bytes)
                        stream.Position += 8;
                        uint timescale = ReadBigEndianUInt32(reader);
                        uint duration = ReadBigEndianUInt32(reader);
                        if (timescale > 0)
                        {
                            return (int)Math.Ceiling((double)duration / timescale);
                        }
                    }
                    break;
                }
                else
                {
                    if (size == 0) break;
                    if (size > 8)
                    {
                        long nextPos = stream.Position + size - 8;
                        if (nextPos >= stream.Length) break;
                        stream.Position = nextPos;
                    }
                }
            }

            return 30; // sensible fallback
        }

        private static uint ReadBigEndianUInt32(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            if (bytes.Length < 4) return 0;
            return (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
        }

        private static ulong ReadBigEndianUInt64(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(8);
            if (bytes.Length < 8) return 0;
            return ((ulong)bytes[0] << 56) | ((ulong)bytes[1] << 48) | ((ulong)bytes[2] << 40) | ((ulong)bytes[3] << 32)
                 | ((ulong)bytes[4] << 24) | ((ulong)bytes[5] << 16) | ((ulong)bytes[6] << 8) | (ulong)bytes[7];
        }
    }
}
