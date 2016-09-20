using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cofoundry.Domain.Data;
using Cofoundry.Domain.CQS;
using Cofoundry.Core.ErrorLogging;
using Cofoundry.Domain;
using ImageResizer;
using System.Net;
using System.Drawing;
using ImageResizer.Util;

namespace Cofoundry.Plugins.ImageResizing.ImageResizer
{

    /// <summary>
    /// Service for resizing and caching the resulting image.
    /// </summary>
    public class ImageResizerResizedImageAssetFileService : IResizedImageAssetFileService
    {
        #region private member variables

        internal static readonly string IMAGE_ASSET_CACHE_CONTAINER_NAME = "ImageAssetCache";

        private const int MAX_IMG_SIZE = 3200;
        private static readonly string[] SIZE_PROPERTIES = new string[] { "width", "height" };
        private static readonly string[] MAX_SIZE_PROPERTIES = new string[] { "maxwidth", "maxheight" };

        private readonly IFileStoreService _fileService;
        private readonly IQueryExecutor _queryExecutor;
        private readonly IErrorLoggingService _errorLoggingService;

        #endregion

        #region constructor

        public ImageResizerResizedImageAssetFileService(
            IFileStoreService fileService,
            IQueryExecutor queryExecutor,
            IErrorLoggingService errorLoggingService
            )
        {
            _fileService = fileService;
            _queryExecutor = queryExecutor;
            _errorLoggingService = errorLoggingService;
        }

        #endregion

        public Stream Get(IImageAssetRenderable asset, IImageResizeSettings inputSettings)
        {
            if ((inputSettings.Width < 1 && inputSettings.Height < 1)
                || (inputSettings.Width == asset.Width && inputSettings.Height == asset.Height))
            {
                return GetFileStream(asset.ImageAssetId);
            }

            var settings = ConvertSettings(inputSettings);

            var directory = asset.ImageAssetId.ToString();
            var fullFileName = directory + "/" + CreateCacheFileName(settings, asset);
            Stream imageStream = null;
            ValidateSettings(settings);

            if (_fileService.Exists(IMAGE_ASSET_CACHE_CONTAINER_NAME, fullFileName))
            {
                return _fileService.Get(IMAGE_ASSET_CACHE_CONTAINER_NAME, fullFileName);
            }
            else
            {
                imageStream = new MemoryStream();

                using (var originalStream = GetFileStream(asset.ImageAssetId))
                {
                    if (originalStream == null) return null;
                    ImageBuilder.Current.Build(originalStream, imageStream, settings);
                }

                try
                {
                    // Try and create the cache file, but don't throw an error if it fails - it will be attempted again on the next request
                    _fileService.CreateIfNotExists(IMAGE_ASSET_CACHE_CONTAINER_NAME, fullFileName, imageStream);
                }
                catch (Exception ex)
                {
                    if (Debugger.IsAttached)
                    {
                        throw;
                    }
                    else
                    {
                        _errorLoggingService.Log(ex);
                    }
                }

                imageStream.Position = 0;
                return imageStream;
            }
        }

        public void Clear(int imageAssetId)
        {
            var directory = imageAssetId.ToString();
            _fileService.DeleteDirectory(IMAGE_ASSET_CACHE_CONTAINER_NAME, directory);
        }

        #region private methods

        private Stream GetFileStream(int imageAssetId)
        {
            var result = _queryExecutor.GetById<ImageAssetFile>(imageAssetId);

            if (result == null || result.ContentStream == null)
            {
                throw new FileNotFoundException(imageAssetId.ToString());
            }

            return result.ContentStream;
        }

        private ResizeSettings ConvertSettings(IImageResizeSettings inputSettings)
        {
            var resizeSettings = new ResizeSettings();

            if (!string.IsNullOrWhiteSpace(inputSettings.BackgroundColor))
            {
                resizeSettings.BackgroundColor = ParseUtils.ParseColor(inputSettings.BackgroundColor, Color.Transparent);
            }

            resizeSettings.Mode = (FitMode)inputSettings.Mode;
            resizeSettings.Scale = (ScaleMode)inputSettings.Scale;
            resizeSettings.Height = inputSettings.Height;
            resizeSettings.Width = inputSettings.Width;
            resizeSettings.Anchor = (ContentAlignment)inputSettings.Anchor;

            return resizeSettings;
        }

        private void ValidateSettings(ResizeSettings settings)
        {
            ValidateSize(settings, SIZE_PROPERTIES);
            ValidateSize(settings, MAX_SIZE_PROPERTIES);
        }

        private void ValidateSize(ResizeSettings settings, string[] sizeProperties)
        {
            string maxSizeProp = null;
            foreach (var prop in sizeProperties)
            {
                string val = settings[prop];
                int size = 0;
                if (val != null && Int32.TryParse(val, out size) && size > MAX_IMG_SIZE)
                {
                    maxSizeProp = prop;
                }
            }

            if (maxSizeProp != null)
            {
                foreach (var prop in sizeProperties)
                {
                    settings[prop] = prop == maxSizeProp ? MAX_IMG_SIZE.ToString() : null;
                }
            }
        }

        private string CreateCacheFileName(ResizeSettings settings, IImageAssetRenderable asset)
        {
            const string format = "w{0}h{1}c{2}s{3}bg{4}a{5}";
            string s = string.Format(format, settings.Width, settings.Height, settings.Mode, settings.Scale, settings.BackgroundColor, settings.Anchor);
            s = WebUtility.UrlEncode(s);
            return Path.ChangeExtension(s, asset.Extension);
        }

        #endregion
    }
}
