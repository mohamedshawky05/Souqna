using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Souqna.Core.Interfaces;

namespace Souqna.Infrastructure.Services
{
    public class StorageService : IStorageService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<StorageService> _logger;
        private readonly string _privateStorageRoot;

        public StorageService(IWebHostEnvironment env, ILogger<StorageService> logger)
        {
            _env = env;
            _logger = logger;
            // Place private storage outside the web root (wwwroot)
            _privateStorageRoot = Path.Combine(_env.ContentRootPath, "App_Data", "IdentityDocs");
            if (!Directory.Exists(_privateStorageRoot))
            {
                Directory.CreateDirectory(_privateStorageRoot);
            }
        }

        public async Task<string> SavePublicFileAsync(Stream stream, string originalFileName, string folder)
        {
            string ext = Path.GetExtension(originalFileName).ToLowerInvariant();
            string uniqueName = $"{Guid.NewGuid():N}{ext}";
            
            string webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            string targetFolder = Path.Combine(webRoot, "uploads", folder);
            
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            string filePath = Path.Combine(targetFolder, uniqueName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await stream.CopyToAsync(fileStream);
            }

            _logger.LogInformation("Saved public file to {Path}", filePath);
            return $"/uploads/{folder}/{uniqueName}";
        }

        public async Task<string> SavePrivateDocumentAsync(Stream stream, string originalFileName)
        {
            string ext = Path.GetExtension(originalFileName).ToLowerInvariant();
            // Use random server-generated unique key - never user input
            string storageKey = $"{Guid.NewGuid():N}{ext}";
            string filePath = Path.Combine(_privateStorageRoot, storageKey);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await stream.CopyToAsync(fileStream);
            }

            _logger.LogInformation("Saved private verification document with storage key {StorageKey}", storageKey);
            return storageKey;
        }

        public Task<(Stream Stream, string ContentType)?> GetPrivateDocumentAsync(string storageKey)
        {
            // Sanitize storage key to prevent directory traversal
            string safeFileName = Path.GetFileName(storageKey);
            string filePath = Path.Combine(_privateStorageRoot, safeFileName);

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Private document not found for key: {StorageKey}", storageKey);
                return Task.FromResult<(Stream Stream, string ContentType)?>(null);
            }

            string ext = Path.GetExtension(safeFileName).ToLowerInvariant();
            string contentType = ext switch
            {
                ".png" => "image/png",
                ".pdf" => "application/pdf",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };

            Stream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult<(Stream Stream, string ContentType)?>((fileStream, contentType));
        }

        public Task<bool> DeleteFileAsync(string relativePathOrKey)
        {
            try
            {
                // Check if it's a private document key
                string privatePath = Path.Combine(_privateStorageRoot, Path.GetFileName(relativePathOrKey));
                if (File.Exists(privatePath))
                {
                    File.Delete(privatePath);
                    return Task.FromResult(true);
                }

                // Check if it's a public web file
                string webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
                string publicPath = Path.Combine(webRoot, relativePathOrKey.TrimStart('/'));
                if (File.Exists(publicPath))
                {
                    File.Delete(publicPath);
                    return Task.FromResult(true);
                }

                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file {Key}", relativePathOrKey);
                return Task.FromResult(false);
            }
        }
    }
}
