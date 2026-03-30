using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace BootCoupon.Services
{
    /// <summary>
    /// Helper class for loading images with fallback strategies for packaged and unpackaged apps
    /// </summary>
    public static class ImageHelper
    {
        /// <summary>
        /// Loads an image from Assets folder with multiple fallback strategies
        /// Works with: Packaged, Unpackaged, Self-contained, and Single-file publish
        /// </summary>
        /// <param name="relativePath">Relative path within Assets folder (e.g., "AsiaHotelLogo.jpg")</param>
        /// <param name="decodePixelWidth">Optional decode width for optimization</param>
        /// <returns>BitmapImage or null if loading fails</returns>
        public static async Task<BitmapImage?> LoadImageAsync(string relativePath, int decodePixelWidth = 0)
        {
            BitmapImage? bitmap = null;

            // Strategy 1: Try packaged app resource (ms-appx)
            bitmap = await TryLoadFromPackagedResourceAsync(relativePath, decodePixelWidth);
            if (bitmap != null) return bitmap;

            // Strategy 2: Try loading from file system relative to executable
            bitmap = await TryLoadFromFileSystemAsync(relativePath, decodePixelWidth);
            if (bitmap != null) return bitmap;

            // Strategy 3: Try loading from embedded resource
            bitmap = await TryLoadFromEmbeddedResourceAsync(relativePath, decodePixelWidth);
            if (bitmap != null) return bitmap;

            Debug.WriteLine($"❌ Failed to load image: {relativePath} using all strategies");
            return null;
        }

        /// <summary>
        /// Strategy 1: Load from packaged app resources (works for MSIX/packaged apps)
        /// </summary>
        private static async Task<BitmapImage?> TryLoadFromPackagedResourceAsync(string relativePath, int decodePixelWidth)
        {
            try
            {
                var uri = new Uri($"ms-appx:///Assets/{relativePath}");
                var file = await StorageFile.GetFileFromApplicationUriAsync(uri);

                using (IRandomAccessStream stream = await file.OpenReadAsync())
                {
                    var bitmap = new BitmapImage();
                    if (decodePixelWidth > 0)
                    {
                        bitmap.DecodePixelWidth = decodePixelWidth;
                    }
                    await bitmap.SetSourceAsync(stream);

                    // Validate
                    var _ = bitmap.PixelWidth;
                    Debug.WriteLine($"✅ Loaded from packaged resource: {relativePath} (PixelWidth: {bitmap.PixelWidth})");
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Failed to load from packaged resource: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Strategy 2: Load from file system (works for unpackaged, self-contained, single-file)
        /// Searches in multiple locations relative to the executable
        /// </summary>
        private static async Task<BitmapImage?> TryLoadFromFileSystemAsync(string relativePath, int decodePixelWidth)
        {
            try
            {
                // Get base directory (works for all publish modes)
                string baseDir = AppContext.BaseDirectory;

                // Try multiple possible locations
                string[] possiblePaths = new[]
                {
                    Path.Combine(baseDir, "Assets", relativePath),
                    Path.Combine(baseDir, relativePath),
                    Path.Combine(Directory.GetCurrentDirectory(), "Assets", relativePath),
                    Path.Combine(Directory.GetCurrentDirectory(), relativePath)
                };

                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        var file = await StorageFile.GetFileFromPathAsync(path);
                        using (IRandomAccessStream stream = await file.OpenReadAsync())
                        {
                            var bitmap = new BitmapImage();
                            if (decodePixelWidth > 0)
                            {
                                bitmap.DecodePixelWidth = decodePixelWidth;
                            }
                            await bitmap.SetSourceAsync(stream);

                            // Validate
                            var _ = bitmap.PixelWidth;
                            Debug.WriteLine($"✅ Loaded from file system: {path} (PixelWidth: {bitmap.PixelWidth})");
                            return bitmap;
                        }
                    }
                }

                Debug.WriteLine($"⚠️ Image not found in any file system location: {relativePath}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Failed to load from file system: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Strategy 3: Load from embedded resource (fallback for single-file publish)
        /// Requires the image to be set as "Embedded resource" in project file
        /// </summary>
        private static async Task<BitmapImage?> TryLoadFromEmbeddedResourceAsync(string relativePath, int decodePixelWidth)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = $"BootCoupon.Assets.{relativePath.Replace("/", ".").Replace("\\", ".")}";

                using (Stream? resourceStream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (resourceStream == null)
                    {
                        Debug.WriteLine($"⚠️ Embedded resource not found: {resourceName}");
                        return null;
                    }

                    using (var memoryStream = new MemoryStream())
                    {
                        await resourceStream.CopyToAsync(memoryStream);
                        memoryStream.Seek(0, SeekOrigin.Begin);

                        var randomAccessStream = memoryStream.AsRandomAccessStream();
                        var bitmap = new BitmapImage();
                        if (decodePixelWidth > 0)
                        {
                            bitmap.DecodePixelWidth = decodePixelWidth;
                        }
                        await bitmap.SetSourceAsync(randomAccessStream);

                        // Validate
                        var _ = bitmap.PixelWidth;
                        Debug.WriteLine($"✅ Loaded from embedded resource: {resourceName} (PixelWidth: {bitmap.PixelWidth})");
                        return bitmap;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Failed to load from embedded resource: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the full path to an asset file for debugging purposes
        /// </summary>
        public static string GetAssetPath(string relativePath)
        {
            string baseDir = AppContext.BaseDirectory;
            return Path.Combine(baseDir, "Assets", relativePath);
        }
    }
}