using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace TPMSimpleModMaker
{
    internal sealed class ImageProcessingService
    {
        private const int MaxIconSize = 256;
        private const int WallpaperTextureSize = 1024;

        // Multi-size item families use one canonical editor reference size so loading or
        // resetting artwork starts at the same visual scale regardless of selected size.
        // Poster uses Standard Poster; Hanging Sign and Wall Sign use their default Small mappings;
        // rugs use a shared family composition scale across both Small and Large families.
        private const int PosterFamilyReferenceWidth = 469;
        private const int PosterFamilyReferenceHeight = 763;
        private const int HangingSignFamilyReferenceWidth = 816;
        private const int HangingSignFamilyReferenceHeight = 220;
        private const int WallSignFamilyReferenceWidth = 803;
        private const int WallSignFamilyReferenceHeight = 282;
        private const int RugFamilyReferenceWidth = 500;
        private const int RugFamilyReferenceHeight = 500;
        private const int RugFamilyCanvasWidth = 620;
        private const int RugFamilyCanvasHeight = 620;

        private sealed class RugShapeProfile
        {
            public string ShapeKind;
            public float ShapeWidth;
            public float ShapeHeight;
        }

        public string PrepareArtwork(string sourcePath, TemplateDefinition template, string fitMode, ImagePlacementState placement)
        {
            if (template == null)
                return Path.GetFullPath(sourcePath);
            if (!template.ImageProcessingEnabled && !IsWallpaperTemplate(template))
                return Path.GetFullPath(sourcePath);

            string root = Path.Combine(AppInfo.LocalDataRoot, "Prepared");
            Directory.CreateDirectory(root);
            string outputPath = Path.Combine(root, "Artwork_" + Guid.NewGuid().ToString("N") + ".png");

            using (Bitmap bitmap = IsWallpaperTemplate(template)
                ? RenderWallpaperTexture(sourcePath, fitMode, placement)
                : IsRugTemplate(template)
                    ? RenderReplacementRugDesignTexture(sourcePath, template, fitMode, placement)
                : IsHangingSignTemplate(template)
                    ? RenderHangingSignTexture(sourcePath, sourcePath, template, fitMode, placement, fitMode, placement)
                    : IsWallSignTemplate(template)
                        ? RenderWallSignTexture(sourcePath, sourcePath, template, fitMode, placement, fitMode, placement)
                        : RenderTexture(sourcePath, template, fitMode, placement))
            {
                bitmap.Save(outputPath, ImageFormat.Png);
            }

            return outputPath;
        }

        public string PrepareIcon(string originalArtworkPath, string preparedArtworkPath, string customIconPath, TemplateDefinition template, string fitMode, ImagePlacementState placement, string iconMode)
        {
            string root = Path.Combine(AppInfo.LocalDataRoot, "Prepared");
            Directory.CreateDirectory(root);
            string outputPath = Path.Combine(root, "Icon_" + Guid.NewGuid().ToString("N") + ".png");

            using (Bitmap icon = RenderIconForBuild(originalArtworkPath, preparedArtworkPath, customIconPath, template, fitMode, placement, iconMode))
                icon.Save(outputPath, ImageFormat.Png);

            return outputPath;
        }

        public Bitmap CreatePreview(string sourcePath, TemplateDefinition template, string fitMode, ImagePlacementState placement, int maxWidth, int maxHeight, Color guideColor)
        {
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                return null;

            if (IsWallpaperTemplate(template))
            {
                using (Bitmap source = LoadUnlockedBitmap(sourcePath))
                    return CreateWallpaperPreview(source, fitMode, placement, maxWidth, maxHeight, guideColor);
            }

            if (IsPosterTemplate(template))
            {
                using (Bitmap source = LoadUnlockedBitmap(sourcePath))
                    return CreatePosterSizeFamilyPreview(source, template, fitMode, placement, maxWidth, maxHeight, guideColor);
            }

            if (IsRugTemplate(template))
            {
                using (Bitmap source = LoadUnlockedBitmap(sourcePath))
                    return CreateRugFamilyPreview(source, template, fitMode, placement, maxWidth, maxHeight, guideColor);
            }

            if (IsHangingSignTemplate(template))
            {
                using (Bitmap source = LoadUnlockedBitmap(sourcePath))
                    return CreateHangingSignDesignPreview(source, template, fitMode, placement, maxWidth, maxHeight, guideColor);
            }

            if (IsWallSignTemplate(template))
            {
                using (Bitmap source = LoadUnlockedBitmap(sourcePath))
                    return CreateWallSignDesignPreview(source, template, fitMode, placement, maxWidth, maxHeight, guideColor);
            }

            // Banner artwork occupies only a narrow mapped region of the final 1024 atlas.
            // Preview the mapped banner face itself rather than the whole atlas so the artwork
            // is centred in the editor. Fantasy also uses its actual notched banner silhouette.
            if (IsBannerTemplate(template))
            {
                using (Bitmap source = LoadUnlockedBitmap(sourcePath))
                    return CreateBannerDesignPreview(source, template, fitMode, placement, maxWidth, maxHeight, guideColor);
            }

            Bitmap rendered;
            if (template != null && template.ImageProcessingEnabled)
                rendered = RenderTexture(sourcePath, template, fitMode, placement);
            else
                rendered = LoadUnlockedBitmap(sourcePath);

            try
            {
                return ResizeForPreview(rendered, maxWidth, maxHeight, template != null && template.ImageProcessingEnabled, template, guideColor);
            }
            finally
            {
                rendered.Dispose();
            }
        }

        public Bitmap CreateInteractivePreview(Image source, TemplateDefinition template, string fitMode, ImagePlacementState placement, int maxWidth, int maxHeight, Color guideColor)
        {
            if (source == null || template == null || maxWidth <= 0 || maxHeight <= 0)
                return null;

            if (IsWallpaperTemplate(template))
                return CreateWallpaperPreview(source, fitMode, placement, maxWidth, maxHeight, guideColor);

            if (!template.ImageProcessingEnabled)
            {
                Bitmap copy = new Bitmap(source);
                try
                {
                    return ResizeSimple(copy, maxWidth, maxHeight, Color.FromArgb(36, 36, 36));
                }
                finally
                {
                    copy.Dispose();
                }
            }

            if (IsPosterTemplate(template))
                return CreatePosterSizeFamilyPreview(source, template, fitMode, placement, maxWidth, maxHeight, guideColor);

            if (IsRugTemplate(template))
                return CreateInteractiveRugPreview(source, template, fitMode, placement, maxWidth, maxHeight, guideColor);

            // Hanging Sign must use the same mapped design-surface preview for both
            // committed and interactive rendering. Previously live pan/zoom fell through
            // to the generic 1024 atlas preview, causing the guide and image scale to jump.
            if (IsHangingSignTemplate(template))
                return CreateHangingSignDesignPreview(source, template, fitMode, placement, maxWidth, maxHeight, guideColor);

            if (IsWallSignTemplate(template))
                return CreateWallSignDesignPreview(source, template, fitMode, placement, maxWidth, maxHeight, guideColor);

            if (IsBannerTemplate(template))
                return CreateBannerDesignPreview(source, template, fitMode, placement, maxWidth, maxHeight, guideColor);

            if (template.TextureWidth <= 0 || template.TextureHeight <= 0 || template.VisibleWidth <= 0 || template.VisibleHeight <= 0)
                return null;

            double scale = Math.Min((double)maxWidth / template.TextureWidth, (double)maxHeight / template.TextureHeight);
            scale = Math.Min(1.0, scale);
            int width = Math.Max(1, (int)Math.Round(template.TextureWidth * scale));
            int height = Math.Max(1, (int)Math.Round(template.TextureHeight * scale));

            Bitmap preview = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(preview))
            {
                ConfigureInteractiveGraphics(g);
                g.Clear(Color.Black);

                Rectangle target = new Rectangle(
                    (int)Math.Round(template.VisibleX * scale),
                    (int)Math.Round(template.VisibleY * scale),
                    Math.Max(1, (int)Math.Round(template.VisibleWidth * scale)),
                    Math.Max(1, (int)Math.Round(template.VisibleHeight * scale)));

                DrawImage(g, source, target, fitMode, ScalePlacement(placement, (float)scale));
                DrawGuideOverlay(g, template, scale, guideColor);
            }
            return preview;
        }

        private static Bitmap CreateInteractiveRugPreview(Image source, TemplateDefinition template, string fitMode, ImagePlacementState placement, int maxWidth, int maxHeight, Color guideColor)
        {
            return CreateRugFamilyPreview(source, template, fitMode, placement, maxWidth, maxHeight, guideColor);
        }

        private static ImagePlacementState ScalePlacement(ImagePlacementState placement, float scale)
        {
            ImagePlacementState scaled = new ImagePlacementState();
            if (placement == null)
                return scaled;

            scaled.Zoom = placement.Zoom;
            scaled.OffsetX = placement.OffsetX * scale;
            scaled.OffsetY = placement.OffsetY * scale;
            scaled.RotationDegrees = placement.RotationDegrees;
            scaled.GuideColorName = placement.GuideColorName;
            scaled.ReferenceWidth = placement.ReferenceWidth > 0 ? Math.Max(1, (int)Math.Round(placement.ReferenceWidth * scale)) : 0;
            scaled.ReferenceHeight = placement.ReferenceHeight > 0 ? Math.Max(1, (int)Math.Round(placement.ReferenceHeight * scale)) : 0;
            return scaled;
        }

        public void EnsureSizeFamilyReference(TemplateDefinition template, ImagePlacementState placement)
        {
            if (template == null || placement == null || placement.ReferenceWidth > 0 && placement.ReferenceHeight > 0)
                return;

            if (IsPosterTemplate(template))
            {
                placement.ReferenceWidth = PosterFamilyReferenceWidth;
                placement.ReferenceHeight = PosterFamilyReferenceHeight;
                return;
            }

            if (IsHangingSignTemplate(template))
            {
                placement.ReferenceWidth = HangingSignFamilyReferenceWidth;
                placement.ReferenceHeight = HangingSignFamilyReferenceHeight;
                return;
            }

            if (IsWallSignTemplate(template))
            {
                placement.ReferenceWidth = WallSignFamilyReferenceWidth;
                placement.ReferenceHeight = WallSignFamilyReferenceHeight;
                return;
            }

            if (IsRugTemplate(template))
            {
                placement.ReferenceWidth = RugFamilyReferenceWidth;
                placement.ReferenceHeight = RugFamilyReferenceHeight;
            }
        }

        public float GetPreviewPixelsPerTexturePixel(TemplateDefinition template, int previewImageWidth, int previewImageHeight)
        {
            if (template == null || previewImageWidth <= 0 || previewImageHeight <= 0)
                return 0f;

            if (IsWallpaperTemplate(template))
            {
                // Wallpaper composition is always authored against one fixed 1024 x 1024
                // texture, irrespective of the dimensions of the imported source image.
                return Math.Min((float)previewImageWidth / WallpaperTextureSize, (float)previewImageHeight / WallpaperTextureSize);
            }

            if (IsRugTemplate(template))
            {
                // Rug previews now render on one shared family canvas so the same source
                // image loads/resets at a consistent scale for Small and Large rugs.
                return Math.Min((float)previewImageWidth / RugFamilyCanvasWidth, (float)previewImageHeight / RugFamilyCanvasHeight);
            }

            if (IsHangingSignTemplate(template))
            {
                // Hanging Sign editor uses one fixed family canvas (the maximum UV face) so
                // one pan pixel always represents the same amount regardless of Small/Large.
                const int familyWidth = 1005;
                const int familyHeight = 222;
                float signScaleX = (float)previewImageWidth / familyWidth;
                float signScaleY = (float)previewImageHeight / familyHeight;
                return Math.Min(signScaleX, signScaleY);
            }

            if (IsBannerTemplate(template) && template.VisibleWidth > 0 && template.VisibleHeight > 0)
            {
                float bannerScaleX = (float)previewImageWidth / (float)template.VisibleWidth;
                float bannerScaleY = (float)previewImageHeight / (float)template.VisibleHeight;
                return Math.Min(bannerScaleX, bannerScaleY);
            }

            if (template.TextureWidth <= 0 || template.TextureHeight <= 0)
                return 0f;
            float scaleX = (float)previewImageWidth / (float)template.TextureWidth;
            float scaleY = (float)previewImageHeight / (float)template.TextureHeight;
            return Math.Min(scaleX, scaleY);
        }

        public Bitmap CreateIconPreview(string artworkPath, string customIconPath, TemplateDefinition template, string fitMode, ImagePlacementState placement, string iconMode, int maxWidth, int maxHeight)
        {
            if (string.Equals(iconMode, "Custom Icon File", StringComparison.OrdinalIgnoreCase) && !File.Exists(customIconPath))
                return null;
            if (!string.Equals(iconMode, "Custom Icon File", StringComparison.OrdinalIgnoreCase) && !File.Exists(artworkPath))
                return null;

            using (Bitmap icon = RenderIconFromSourcePaths(artworkPath, customIconPath, template, fitMode, placement, iconMode))
            {
                return ResizeSimple(icon, maxWidth, maxHeight, Color.Transparent);
            }
        }

        public string PrepareDoubleBannerArtwork(
            string leftArtworkPath,
            string rightArtworkPath,
            TemplateDefinition template,
            string leftFitMode,
            ImagePlacementState leftPlacement,
            string rightFitMode,
            ImagePlacementState rightPlacement)
        {
            if (!IsDualArtworkTemplate(template))
                throw new ArgumentException("The selected template does not use Front/Back or Left/Right artwork.", "template");
            if (string.IsNullOrEmpty(leftArtworkPath) || !File.Exists(leftArtworkPath))
                throw new FileNotFoundException("Primary artwork could not be found.", leftArtworkPath);
            if (string.IsNullOrEmpty(rightArtworkPath) || !File.Exists(rightArtworkPath))
                throw new FileNotFoundException("Secondary artwork could not be found.", rightArtworkPath);

            string root = Path.Combine(AppInfo.LocalDataRoot, "Prepared");
            Directory.CreateDirectory(root);
            string outputPath = Path.Combine(root, "DoubleBannerArtwork_" + Guid.NewGuid().ToString("N") + ".png");
            using (Bitmap bitmap = RenderDoubleBannerTexture(leftArtworkPath, rightArtworkPath, template,
                leftFitMode, leftPlacement, rightFitMode, rightPlacement))
            {
                bitmap.Save(outputPath, ImageFormat.Png);
            }
            return outputPath;
        }

        public string PrepareDoubleBannerIcon(
            string leftArtworkPath,
            string rightArtworkPath,
            string preparedArtworkPath,
            string customIconPath,
            TemplateDefinition template,
            string leftFitMode,
            ImagePlacementState leftPlacement,
            string rightFitMode,
            ImagePlacementState rightPlacement,
            string iconMode)
        {
            string root = Path.Combine(AppInfo.LocalDataRoot, "Prepared");
            Directory.CreateDirectory(root);
            string outputPath = Path.Combine(root, "DoubleBannerIcon_" + Guid.NewGuid().ToString("N") + ".png");
            using (Bitmap icon = RenderDoubleBannerIcon(leftArtworkPath, rightArtworkPath, preparedArtworkPath, customIconPath,
                template, leftFitMode, leftPlacement, rightFitMode, rightPlacement, iconMode))
            {
                icon.Save(outputPath, ImageFormat.Png);
            }
            return outputPath;
        }

        public Bitmap CreateDoubleBannerIconPreview(
            string leftArtworkPath,
            string rightArtworkPath,
            string customIconPath,
            TemplateDefinition template,
            string leftFitMode,
            ImagePlacementState leftPlacement,
            string rightFitMode,
            ImagePlacementState rightPlacement,
            string iconMode,
            int maxWidth,
            int maxHeight)
        {
            string mode = NormalizeIconMode(iconMode);
            if (string.Equals(mode, "Custom Icon File", StringComparison.OrdinalIgnoreCase))
            {
                if (!File.Exists(customIconPath)) return null;
            }
            else if (!File.Exists(leftArtworkPath) && !File.Exists(rightArtworkPath))
            {
                return null;
            }

            using (Bitmap icon = RenderDoubleBannerIcon(leftArtworkPath, rightArtworkPath, null, customIconPath,
                template, leftFitMode, leftPlacement, rightFitMode, rightPlacement, iconMode))
            {
                return ResizeSimple(icon, maxWidth, maxHeight, Color.Transparent);
            }
        }

        private static Bitmap RenderDoubleBannerTexture(
            string leftArtworkPath,
            string rightArtworkPath,
            TemplateDefinition template,
            string leftFitMode,
            ImagePlacementState leftPlacement,
            string rightFitMode,
            ImagePlacementState rightPlacement)
        {
            if (IsHangingSignTemplate(template))
                return RenderHangingSignTexture(leftArtworkPath, rightArtworkPath, template, leftFitMode, leftPlacement, rightFitMode, rightPlacement);
            if (IsWallSignTemplate(template))
                return RenderWallSignTexture(leftArtworkPath, rightArtworkPath, template, leftFitMode, leftPlacement, rightFitMode, rightPlacement);

            bool fantasy = IsFantasyDoubleBannerTemplate(template);
            Rectangle leftTarget = fantasy
                ? new Rectangle(89, 119, 332, 896)
                : new Rectangle(70, 4, 373, 1017);
            Rectangle rightTarget = fantasy
                ? new Rectangle(601, 119, 332, 896)
                : new Rectangle(580, 4, 373, 1017);

            using (Bitmap left = LoadUnlockedBitmap(leftArtworkPath))
            using (Bitmap right = LoadUnlockedBitmap(rightArtworkPath))
            {
                Bitmap output = new Bitmap(1024, 1024, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(output))
                {
                    ConfigureGraphics(g);
                    g.Clear(Color.Black);
                    DrawImage(g, left, leftTarget, leftFitMode, leftPlacement);
                    DrawImage(g, right, rightTarget, rightFitMode, rightPlacement);
                }
                return output;
            }
        }

        private static Bitmap RenderDoubleBannerIcon(
            string leftArtworkPath,
            string rightArtworkPath,
            string preparedArtworkPath,
            string customIconPath,
            TemplateDefinition template,
            string leftFitMode,
            ImagePlacementState leftPlacement,
            string rightFitMode,
            ImagePlacementState rightPlacement,
            string iconMode)
        {
            string mode = NormalizeIconMode(iconMode);
            if (string.Equals(mode, "Custom Icon File", StringComparison.OrdinalIgnoreCase))
            {
                using (Bitmap custom = LoadUnlockedBitmap(customIconPath))
                    return RenderBasicSquareIcon(custom);
            }

            if (string.Equals(mode, "Final Texture", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(preparedArtworkPath) && File.Exists(preparedArtworkPath))
            {
                using (Bitmap prepared = LoadUnlockedBitmap(preparedArtworkPath))
                    return RenderBasicSquareIcon(prepared);
            }

            using (Bitmap left = LoadOptionalArtwork(leftArtworkPath))
            using (Bitmap right = LoadOptionalArtwork(rightArtworkPath))
            {
                if (IsHangingSignTemplate(template))
                {
                    if (string.Equals(mode, "Original Artwork", StringComparison.OrdinalIgnoreCase))
                        return RenderDualArtworkSquareIconVertical(left, right);

                    using (Bitmap frontDesign = CreateHangingSignSurface(left, template, leftFitMode, leftPlacement))
                        return RenderHangingSignMaskedIcon(frontDesign, template);
                }

                if (IsWallSignTemplate(template))
                {
                    if (string.Equals(mode, "Original Artwork", StringComparison.OrdinalIgnoreCase))
                        return RenderDualArtworkSquareIconVertical(left, right);

                    using (Bitmap frontDesign = CreateWallSignSurface(left, template, leftFitMode, leftPlacement))
                        return RenderWallSignMaskedIcon(frontDesign, template);
                }

                if (string.Equals(mode, "Original Artwork", StringComparison.OrdinalIgnoreCase))
                    return RenderDualArtworkSquareIcon(right, left);

                bool fantasy = IsFantasyDoubleBannerTemplate(template);
                int designWidth = fantasy ? 332 : 373;
                int designHeight = fantasy ? 896 : 1017;
                using (Bitmap leftDesign = CreateBannerDesignSurfaceForDimensions(left, fantasy, designWidth, designHeight, leftFitMode, leftPlacement))
                using (Bitmap rightDesign = CreateBannerDesignSurfaceForDimensions(right, fantasy, designWidth, designHeight, rightFitMode, rightPlacement))
                    return RenderDoubleBannerMaskedIcon(rightDesign, leftDesign, fantasy);
            }
        }

        private static Bitmap LoadOptionalArtwork(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                return LoadUnlockedBitmap(path);

            Bitmap blank = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
            blank.SetPixel(0, 0, Color.Transparent);
            return blank;
        }

        private static Bitmap CreateBannerDesignSurfaceForDimensions(Image source, bool fantasy, int width, int height, string fitMode, ImagePlacementState placement)
        {
            Bitmap design = new Bitmap(Math.Max(1, width), Math.Max(1, height), PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(design))
            {
                ConfigureGraphics(g);
                g.Clear(Color.Transparent);
                Rectangle bounds = new Rectangle(0, 0, design.Width, design.Height);
                using (GraphicsPath shape = CreateBannerPreviewPath(fantasy,
                    new RectangleF(1f, 1f, Math.Max(1f, design.Width - 2f), Math.Max(1f, design.Height - 2f))))
                {
                    GraphicsState state = g.Save();
                    g.SetClip(shape);
                    DrawImage(g, source, bounds, fitMode, placement);
                    g.Restore(state);
                }
            }
            return design;
        }

        private static Bitmap RenderDualArtworkSquareIcon(Image left, Image right)
        {
            Bitmap output = new Bitmap(MaxIconSize, MaxIconSize, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(output))
            {
                ConfigureGraphics(g);
                g.Clear(Color.Transparent);
                Rectangle leftBox = new Rectangle(0, 0, MaxIconSize / 2, MaxIconSize);
                Rectangle rightBox = new Rectangle(MaxIconSize / 2, 0, MaxIconSize - (MaxIconSize / 2), MaxIconSize);
                g.DrawImage(left, FillRectangle(left.Width, left.Height, leftBox));
                g.DrawImage(right, FillRectangle(right.Width, right.Height, rightBox));
            }
            return output;
        }

        private static Bitmap RenderDoubleBannerMaskedIcon(Image leftDesign, Image rightDesign, bool fantasy)
        {
            string maskFile = fantasy ? "double_banner_fantasy_icon_mask.png" : "double_banner_general_icon_mask.png";
            string maskPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Theme", maskFile);
            if (!File.Exists(maskPath))
                return RenderDualArtworkSquareIcon(leftDesign, rightDesign);

            using (Bitmap rawMask = LoadUnlockedBitmap(maskPath))
            {
                Bitmap canvas = new Bitmap(MaxIconSize, MaxIconSize, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(canvas))
                {
                    ConfigureGraphics(g);
                    g.Clear(Color.Transparent);
                    Rectangle fitted = FitRectangle(rawMask.Width, rawMask.Height,
                        new Rectangle(4, 4, MaxIconSize - 8, MaxIconSize - 8), false);
                    g.DrawImage(rawMask, fitted);
                }

                int splitX = canvas.Width / 2;
                Rectangle leftRed = FindMaskRedBounds(canvas, 0, splitX);
                Rectangle rightRed = FindMaskRedBounds(canvas, splitX, canvas.Width);
                using (Bitmap leftLayer = RenderArtworkLayer(leftDesign, canvas.Size, leftRed))
                using (Bitmap rightLayer = RenderArtworkLayer(rightDesign, canvas.Size, rightRed))
                {
                    for (int y = 0; y < canvas.Height; y++)
                    {
                        for (int x = 0; x < canvas.Width; x++)
                        {
                            Color mask = canvas.GetPixel(x, y);
                            if (!IsBannerMaskRed(mask)) continue;
                            Bitmap layer = x < splitX ? leftLayer : rightLayer;
                            Color art = layer.GetPixel(x, y);
                            int alpha = Math.Min(mask.A, art.A);
                            canvas.SetPixel(x, y, Color.FromArgb(alpha, art.R, art.G, art.B));
                        }
                    }
                }
                return canvas;
            }
        }

        private static Rectangle FindMaskRedBounds(Bitmap mask, int minX, int maxX)
        {
            Rectangle bounds = Rectangle.Empty;
            for (int y = 0; y < mask.Height; y++)
            {
                for (int x = Math.Max(0, minX); x < Math.Min(mask.Width, maxX); x++)
                {
                    if (!IsBannerMaskRed(mask.GetPixel(x, y))) continue;
                    Rectangle pixel = new Rectangle(x, y, 1, 1);
                    bounds = bounds.IsEmpty ? pixel : Rectangle.Union(bounds, pixel);
                }
            }
            return bounds;
        }

        private static Bitmap RenderArtworkLayer(Image artwork, Size canvasSize, Rectangle target)
        {
            Bitmap layer = new Bitmap(canvasSize.Width, canvasSize.Height, PixelFormat.Format32bppArgb);
            if (target.IsEmpty) return layer;
            using (Graphics g = Graphics.FromImage(layer))
            {
                ConfigureGraphics(g);
                g.Clear(Color.Transparent);
                g.DrawImage(artwork, FillRectangle(artwork.Width, artwork.Height, target));
            }
            return layer;
        }

        private static bool IsDoubleBannerTemplate(TemplateDefinition template)
        {
            return template != null && !string.IsNullOrEmpty(template.Key) &&
                template.Key.EndsWith(" Double Banner", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHangingSignTemplate(TemplateDefinition template)
        {
            return template != null && !string.IsNullOrEmpty(template.Key) &&
                template.Key.EndsWith(" Hanging Sign", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWallSignTemplate(TemplateDefinition template)
        {
            return template != null && !string.IsNullOrEmpty(template.Key) &&
                template.Key.EndsWith(" Wall Sign", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDualArtworkTemplate(TemplateDefinition template)
        {
            return IsDoubleBannerTemplate(template) || IsHangingSignTemplate(template) || IsWallSignTemplate(template);
        }

        private static bool IsFantasyDoubleBannerTemplate(TemplateDefinition template)
        {
            return IsDoubleBannerTemplate(template) && template.Key.IndexOf("Fantasy", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Bitmap CreatePosterSizeFamilyPreview(Image source, TemplateDefinition template, string fitMode, ImagePlacementState placement, int maxWidth, int maxHeight, Color guideColor)
        {
            if (source == null || template == null || maxWidth <= 0 || maxHeight <= 0)
                return null;

            EnsureFamilyReferenceInternal(template, placement);
            const int canvasWidth = 1024;
            const int canvasHeight = 1024;
            Bitmap canvas = new Bitmap(canvasWidth, canvasHeight, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(canvas))
            {
                ConfigureGraphics(g);
                g.Clear(Color.Black);

                Rectangle guide = new Rectangle(template.VisibleX, template.VisibleY, template.VisibleWidth, template.VisibleHeight);
                Rectangle reference = CentreReferenceRectangle(guide, placement);
                DrawImageWithoutClip(g, source, reference, fitMode, placement);
                DrawDimmedGuide(g, new Rectangle(0, 0, canvasWidth, canvasHeight), guide, guideColor);
            }

            try
            {
                return ResizeSimple(canvas, maxWidth, maxHeight, Color.FromArgb(36, 36, 36));
            }
            finally
            {
                canvas.Dispose();
            }
        }

        private static Bitmap CreateHangingSignDesignPreview(Image source, TemplateDefinition template, string fitMode, ImagePlacementState placement, int maxWidth, int maxHeight, Color guideColor)
        {
            if (source == null || template == null || maxWidth <= 0 || maxHeight <= 0)
                return null;

            EnsureFamilyReferenceInternal(template, placement);
            const int familyWidth = 1005;
            const int familyHeight = 222;

            Rectangle currentTarget;
            Rectangle ignored;
            GetHangingSignTargets(template, out currentTarget, out ignored);
            Rectangle guide = CentreRectangle(new Rectangle(0, 0, familyWidth, familyHeight), currentTarget.Width, currentTarget.Height);
            Rectangle reference = CentreReferenceRectangle(guide, placement);

            Bitmap canvas = new Bitmap(familyWidth, familyHeight, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(canvas))
            {
                ConfigureGraphics(g);
                g.Clear(Color.Black);
                DrawImageWithoutClip(g, source, reference, fitMode, placement);
                DrawDimmedGuide(g, new Rectangle(0, 0, familyWidth, familyHeight), guide, guideColor);
            }

            try
            {
                return ResizeSimple(canvas, maxWidth, maxHeight, Color.FromArgb(36, 36, 36));
            }
            finally
            {
                canvas.Dispose();
            }
        }

        private static Bitmap CreateHangingSignSurface(Image source, TemplateDefinition template, string fitMode, ImagePlacementState placement)
        {
            Rectangle frontTarget;
            Rectangle backTarget;
            GetHangingSignTargets(template, out frontTarget, out backTarget);
            EnsureFamilyReferenceInternal(template, placement);

            Bitmap design = new Bitmap(Math.Max(1, frontTarget.Width), Math.Max(1, frontTarget.Height), PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(design))
            {
                ConfigureGraphics(g);
                g.Clear(Color.Black);
                Rectangle bounds = new Rectangle(0, 0, design.Width, design.Height);
                Rectangle reference = CentreReferenceRectangle(bounds, placement);
                DrawImageWithoutClip(g, source, reference, fitMode, placement);
            }
            return design;
        }

        private static Bitmap CreateWallSignDesignPreview(Image source, TemplateDefinition template, string fitMode, ImagePlacementState placement, int maxWidth, int maxHeight, Color guideColor)
        {
            if (source == null || template == null || maxWidth <= 0 || maxHeight <= 0)
                return null;

            EnsureFamilyReferenceInternal(template, placement);
            const int familyWidth = 995;
            const int familyHeight = 283;

            Rectangle currentTarget;
            Rectangle ignored;
            GetWallSignTargets(template, out currentTarget, out ignored);
            Rectangle guide = CentreRectangle(new Rectangle(0, 0, familyWidth, familyHeight), currentTarget.Width, currentTarget.Height);
            Rectangle reference = CentreReferenceRectangle(guide, placement);

            Bitmap canvas = new Bitmap(familyWidth, familyHeight, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(canvas))
            {
                ConfigureGraphics(g);
                g.Clear(Color.Black);
                DrawImageWithoutClip(g, source, reference, fitMode, placement);
                DrawDimmedGuide(g, new Rectangle(0, 0, familyWidth, familyHeight), guide, guideColor);
            }

            try
            {
                return ResizeSimple(canvas, maxWidth, maxHeight, Color.FromArgb(36, 36, 36));
            }
            finally
            {
                canvas.Dispose();
            }
        }

        private static Bitmap CreateWallSignSurface(Image source, TemplateDefinition template, string fitMode, ImagePlacementState placement)
        {
            Rectangle frontTarget;
            Rectangle backTarget;
            GetWallSignTargets(template, out frontTarget, out backTarget);
            EnsureFamilyReferenceInternal(template, placement);

            Bitmap design = new Bitmap(Math.Max(1, frontTarget.Width), Math.Max(1, frontTarget.Height), PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(design))
            {
                ConfigureGraphics(g);
                g.Clear(Color.Black);
                Rectangle bounds = new Rectangle(0, 0, design.Width, design.Height);
                Rectangle reference = CentreReferenceRectangle(bounds, placement);
                DrawImageWithoutClip(g, source, reference, fitMode, placement);
            }
            return design;
        }

        private static Bitmap RenderWallSignTexture(string frontArtworkPath, string backArtworkPath, TemplateDefinition template, string frontFitMode, ImagePlacementState frontPlacement, string backFitMode, ImagePlacementState backPlacement)
        {
            Rectangle frontTarget;
            Rectangle backTarget;
            GetWallSignTargets(template, out frontTarget, out backTarget);

            using (Bitmap front = LoadUnlockedBitmap(frontArtworkPath))
            using (Bitmap back = LoadUnlockedBitmap(backArtworkPath))
            {
                Bitmap output = new Bitmap(1024, 1024, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(output))
                {
                    ConfigureGraphics(g);
                    g.Clear(Color.Black);
                    EnsureFamilyReferenceInternal(template, frontPlacement);
                    EnsureFamilyReferenceInternal(template, backPlacement);
                    DrawReferencedImageClipped(g, front, frontTarget, frontFitMode, frontPlacement);
                    DrawReferencedImageClipped(g, back, backTarget, backFitMode, backPlacement);
                }
                return output;
            }
        }

        private static void GetWallSignTargets(TemplateDefinition template, out Rectangle frontTarget, out Rectangle backTarget)
        {
            // Verified against the supplied SmallWallSign-UVW.png and LargeWallSign-UVW.png.
            if (template != null && !string.IsNullOrEmpty(template.Key) && template.Key.StartsWith("Large ", StringComparison.OrdinalIgnoreCase))
            {
                frontTarget = new Rectangle(15, 101, 995, 283);
                backTarget = new Rectangle(15, 640, 995, 283);
            }
            else
            {
                frontTarget = new Rectangle(106, 102, 803, 282);
                backTarget = new Rectangle(106, 640, 803, 282);
            }
        }

        private static Bitmap RenderWallSignMaskedIcon(Image frontDesign, TemplateDefinition template)
        {
            string maskFile = template != null && !string.IsNullOrEmpty(template.Key) && template.Key.StartsWith("Large ", StringComparison.OrdinalIgnoreCase)
                ? "wall_sign_large_icon_mask.png"
                : "wall_sign_small_icon_mask.png";
            string maskPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Theme", maskFile);
            if (!File.Exists(maskPath))
                return RenderBasicSquareIcon(frontDesign);

            using (Bitmap rawMask = LoadUnlockedBitmap(maskPath))
            {
                Bitmap maskCanvas = new Bitmap(MaxIconSize, MaxIconSize, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(maskCanvas))
                {
                    ConfigureGraphics(g);
                    g.Clear(Color.Transparent);
                    g.DrawImage(rawMask, new Rectangle(0, 0, MaxIconSize, MaxIconSize));
                }

                PointF topLeft;
                PointF topRight;
                PointF bottomRight;
                PointF bottomLeft;
                GetWallSignIconFaceQuad(template, rawMask.Width, rawMask.Height,
                    out topLeft, out topRight, out bottomRight, out bottomLeft);

                float scaleX = (float)MaxIconSize / Math.Max(1, rawMask.Width);
                float scaleY = (float)MaxIconSize / Math.Max(1, rawMask.Height);
                topLeft = new PointF(topLeft.X * scaleX, topLeft.Y * scaleY);
                topRight = new PointF(topRight.X * scaleX, topRight.Y * scaleY);
                bottomRight = new PointF(bottomRight.X * scaleX, bottomRight.Y * scaleY);
                bottomLeft = new PointF(bottomLeft.X * scaleX, bottomLeft.Y * scaleY);

                double[] inverse = BuildInverseUnitSquareToQuadTransform(topLeft, topRight, bottomRight, bottomLeft);
                if (inverse == null)
                    return maskCanvas;

                for (int y = 0; y < maskCanvas.Height; y++)
                {
                    for (int x = 0; x < maskCanvas.Width; x++)
                    {
                        Color maskPixel = maskCanvas.GetPixel(x, y);
                        if (!IsBannerMaskRed(maskPixel))
                            continue;

                        double denominator = (inverse[6] * (x + 0.5)) + (inverse[7] * (y + 0.5)) + inverse[8];
                        if (Math.Abs(denominator) < 0.0000001)
                        {
                            maskCanvas.SetPixel(x, y, Color.Transparent);
                            continue;
                        }

                        double u = ((inverse[0] * (x + 0.5)) + (inverse[1] * (y + 0.5)) + inverse[2]) / denominator;
                        double v = ((inverse[3] * (x + 0.5)) + (inverse[4] * (y + 0.5)) + inverse[5]) / denominator;
                        if (u < 0.0 || u > 1.0 || v < 0.0 || v > 1.0)
                        {
                            maskCanvas.SetPixel(x, y, Color.Transparent);
                            continue;
                        }

                        Color artPixel = SampleBilinear(frontDesign,
                            u * Math.Max(0, frontDesign.Width - 1),
                            v * Math.Max(0, frontDesign.Height - 1));
                        int alpha = Math.Min(maskPixel.A, artPixel.A);
                        maskCanvas.SetPixel(x, y, Color.FromArgb(alpha, artPixel.R, artPixel.G, artPixel.B));
                    }
                }

                return maskCanvas;
            }
        }

        private static void GetWallSignIconFaceQuad(TemplateDefinition template, int maskWidth, int maskHeight,
            out PointF topLeft, out PointF topRight, out PointF bottomRight, out PointF bottomLeft)
        {
            bool large = template != null && !string.IsNullOrEmpty(template.Key) &&
                template.Key.StartsWith("Large ", StringComparison.OrdinalIgnoreCase);

            // Coordinates are measured directly from the supplied 550x550 perspective masks.
            // They describe the visible inner Sign face, not the oversized red chroma rectangle
            // behind the frame. Scaling below keeps the mapping valid if the source mask size changes.
            float sourceWidth = 550f;
            float sourceHeight = 550f;
            float sx = Math.Max(1, maskWidth) / sourceWidth;
            float sy = Math.Max(1, maskHeight) / sourceHeight;

            if (large)
            {
                topLeft = new PointF(65f * sx, 259f * sy);
                topRight = new PointF(462f * sx, 175f * sy);
                bottomRight = new PointF(461f * sx, 292f * sy);
                bottomLeft = new PointF(69f * sx, 383f * sy);
            }
            else
            {
                topLeft = new PointF(105f * sx, 250f * sy);
                topRight = new PointF(421f * sx, 183f * sy);
                bottomRight = new PointF(421f * sx, 301f * sy);
                bottomLeft = new PointF(105f * sx, 375f * sy);
            }
        }

        private static double[] BuildInverseUnitSquareToQuadTransform(PointF p0, PointF p1, PointF p2, PointF p3)
        {
            double dx1 = p1.X - p2.X;
            double dx2 = p3.X - p2.X;
            double dx3 = p0.X - p1.X + p2.X - p3.X;
            double dy1 = p1.Y - p2.Y;
            double dy2 = p3.Y - p2.Y;
            double dy3 = p0.Y - p1.Y + p2.Y - p3.Y;

            double a;
            double b;
            double c = p0.X;
            double d;
            double e;
            double f = p0.Y;
            double g;
            double h;

            if (Math.Abs(dx3) < 0.0000001 && Math.Abs(dy3) < 0.0000001)
            {
                g = 0.0;
                h = 0.0;
                a = p1.X - p0.X;
                b = p3.X - p0.X;
                d = p1.Y - p0.Y;
                e = p3.Y - p0.Y;
            }
            else
            {
                double denominator = (dx1 * dy2) - (dx2 * dy1);
                if (Math.Abs(denominator) < 0.0000001)
                    return null;

                g = ((dx3 * dy2) - (dx2 * dy3)) / denominator;
                h = ((dx1 * dy3) - (dx3 * dy1)) / denominator;
                a = p1.X - p0.X + (g * p1.X);
                b = p3.X - p0.X + (h * p3.X);
                d = p1.Y - p0.Y + (g * p1.Y);
                e = p3.Y - p0.Y + (h * p3.Y);
            }

            double determinant =
                (a * ((e * 1.0) - (f * h))) -
                (b * ((d * 1.0) - (f * g))) +
                (c * ((d * h) - (e * g)));
            if (Math.Abs(determinant) < 0.0000001)
                return null;

            double inverseDeterminant = 1.0 / determinant;
            return new double[]
            {
                ((e * 1.0) - (f * h)) * inverseDeterminant,
                ((c * h) - (b * 1.0)) * inverseDeterminant,
                ((b * f) - (c * e)) * inverseDeterminant,
                ((f * g) - (d * 1.0)) * inverseDeterminant,
                ((a * 1.0) - (c * g)) * inverseDeterminant,
                ((c * d) - (a * f)) * inverseDeterminant,
                ((d * h) - (e * g)) * inverseDeterminant,
                ((b * g) - (a * h)) * inverseDeterminant,
                ((a * e) - (b * d)) * inverseDeterminant
            };
        }

        private static Color SampleBilinear(Image source, double x, double y)
        {
            Bitmap bitmap = source as Bitmap;
            if (bitmap == null)
            {
                using (Bitmap copy = new Bitmap(source))
                    return SampleBilinear(copy, x, y);
            }

            int x0 = Math.Max(0, Math.Min(bitmap.Width - 1, (int)Math.Floor(x)));
            int y0 = Math.Max(0, Math.Min(bitmap.Height - 1, (int)Math.Floor(y)));
            int x1 = Math.Max(0, Math.Min(bitmap.Width - 1, x0 + 1));
            int y1 = Math.Max(0, Math.Min(bitmap.Height - 1, y0 + 1));
            double tx = Math.Max(0.0, Math.Min(1.0, x - x0));
            double ty = Math.Max(0.0, Math.Min(1.0, y - y0));

            Color c00 = bitmap.GetPixel(x0, y0);
            Color c10 = bitmap.GetPixel(x1, y0);
            Color c01 = bitmap.GetPixel(x0, y1);
            Color c11 = bitmap.GetPixel(x1, y1);

            double w00 = (1.0 - tx) * (1.0 - ty);
            double w10 = tx * (1.0 - ty);
            double w01 = (1.0 - tx) * ty;
            double w11 = tx * ty;

            int a = ClampByte((c00.A * w00) + (c10.A * w10) + (c01.A * w01) + (c11.A * w11));
            int r = ClampByte((c00.R * w00) + (c10.R * w10) + (c01.R * w01) + (c11.R * w11));
            int gr = ClampByte((c00.G * w00) + (c10.G * w10) + (c01.G * w01) + (c11.G * w11));
            int bl = ClampByte((c00.B * w00) + (c10.B * w10) + (c01.B * w01) + (c11.B * w11));
            return Color.FromArgb(a, r, gr, bl);
        }

        private static int ClampByte(double value)
        {
            return Math.Max(0, Math.Min(255, (int)Math.Round(value)));
        }

        private static Bitmap CreateRugFamilyPreview(Image source, TemplateDefinition template, string fitMode, ImagePlacementState placement, int maxWidth, int maxHeight, Color guideColor)
        {
            if (source == null || template == null || maxWidth <= 0 || maxHeight <= 0)
                return null;

            RugShapeProfile profile = GetRugShapeProfile(template.Key);
            if (profile == null || profile.ShapeWidth <= 0 || profile.ShapeHeight <= 0)
                return null;

            EnsureFamilyReferenceInternal(template, placement);
            Rectangle guide = CentreRectangle(new Rectangle(0, 0, RugFamilyCanvasWidth, RugFamilyCanvasHeight),
                Math.Max(1, (int)Math.Round(profile.ShapeWidth)),
                Math.Max(1, (int)Math.Round(profile.ShapeHeight)));

            Bitmap canvas = new Bitmap(RugFamilyCanvasWidth, RugFamilyCanvasHeight, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(canvas))
            using (GraphicsPath shape = CreateRugShapePath(profile.ShapeKind, new RectangleF(guide.X + 1f, guide.Y + 1f, Math.Max(1f, guide.Width - 2f), Math.Max(1f, guide.Height - 2f))))
            {
                ConfigureInteractiveGraphics(g);
                g.Clear(Color.FromArgb(36, 36, 36));

                GraphicsState state = g.Save();
                g.SetClip(shape);
                DrawReferencedImageWithoutClip(g, source, guide, fitMode, placement);
                g.Restore(state);

                using (GraphicsPath outline = CreateRugShapePath(profile.ShapeKind, new RectangleF(guide.X + 2f, guide.Y + 2f, Math.Max(1f, guide.Width - 4f), Math.Max(1f, guide.Height - 4f))))
                using (Pen outer = new Pen(Color.FromArgb(180, 0, 0, 0), 5f))
                using (Pen guidePen = new Pen(guideColor, 2.5f))
                {
                    g.DrawPath(outer, outline);
                    g.DrawPath(guidePen, outline);
                }
            }

            try
            {
                return ResizeSimple(canvas, maxWidth, maxHeight, Color.FromArgb(36, 36, 36));
            }
            finally
            {
                canvas.Dispose();
            }
        }

        private static Bitmap RenderHangingSignTexture(string frontArtworkPath, string backArtworkPath, TemplateDefinition template, string frontFitMode, ImagePlacementState frontPlacement, string backFitMode, ImagePlacementState backPlacement)
        {
            Rectangle frontTarget;
            Rectangle backTarget;
            GetHangingSignTargets(template, out frontTarget, out backTarget);

            using (Bitmap front = LoadUnlockedBitmap(frontArtworkPath))
            using (Bitmap back = LoadUnlockedBitmap(backArtworkPath))
            {
                Bitmap output = new Bitmap(1024, 1024, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(output))
                {
                    ConfigureGraphics(g);
                    g.Clear(Color.Black);
                    EnsureFamilyReferenceInternal(template, frontPlacement);
                    EnsureFamilyReferenceInternal(template, backPlacement);
                    DrawReferencedImageClipped(g, front, frontTarget, frontFitMode, frontPlacement);
                    DrawReferencedImageClipped(g, back, backTarget, backFitMode, backPlacement);
                }
                return output;
            }
        }

        private static void GetHangingSignTargets(TemplateDefinition template, out Rectangle frontTarget, out Rectangle backTarget)
        {
            // Verified directly against the user-supplied named UVW guides:
            // Small_UVW.png = inset artwork bands; Large_UVW.png = near full-width bands.
            if (template != null && !string.IsNullOrEmpty(template.Key) && template.Key.StartsWith("Large ", StringComparison.OrdinalIgnoreCase))
            {
                frontTarget = new Rectangle(9, 162, 1005, 222);
                backTarget = new Rectangle(9, 640, 1005, 222);
            }
            else
            {
                frontTarget = new Rectangle(105, 164, 816, 220);
                backTarget = new Rectangle(105, 640, 816, 220);
            }
        }

        private static Bitmap RenderDualArtworkSquareIconVertical(Image top, Image bottom)
        {
            Bitmap output = new Bitmap(MaxIconSize, MaxIconSize, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(output))
            {
                ConfigureGraphics(g);
                g.Clear(Color.Transparent);
                Rectangle topBox = new Rectangle(0, 0, MaxIconSize, MaxIconSize / 2);
                Rectangle bottomBox = new Rectangle(0, MaxIconSize / 2, MaxIconSize, MaxIconSize - (MaxIconSize / 2));
                g.DrawImage(top, FillRectangle(top.Width, top.Height, topBox));
                g.DrawImage(bottom, FillRectangle(bottom.Width, bottom.Height, bottomBox));
            }
            return output;
        }

        private static Bitmap RenderHangingSignMaskedIcon(Image frontDesign, TemplateDefinition template)
        {
            string maskFile = template != null && !string.IsNullOrEmpty(template.Key) && template.Key.StartsWith("Large ", StringComparison.OrdinalIgnoreCase)
                ? "hanging_sign_large_icon_mask.png"
                : "hanging_sign_small_icon_mask.png";
            string maskPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Theme", maskFile);
            if (!File.Exists(maskPath))
                return RenderBasicSquareIcon(frontDesign);

            using (Bitmap rawMask = LoadUnlockedBitmap(maskPath))
            {
                Bitmap maskCanvas = new Bitmap(MaxIconSize, MaxIconSize, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(maskCanvas))
                {
                    ConfigureGraphics(g);
                    g.Clear(Color.Transparent);
                    g.DrawImage(rawMask, new Rectangle(0, 0, MaxIconSize, MaxIconSize));
                }

                Rectangle redBounds = Rectangle.Empty;
                for (int y = 0; y < maskCanvas.Height; y++)
                {
                    for (int x = 0; x < maskCanvas.Width; x++)
                    {
                        Color c = maskCanvas.GetPixel(x, y);
                        if (!IsBannerMaskRed(c))
                            continue;
                        Rectangle pixel = new Rectangle(x, y, 1, 1);
                        redBounds = redBounds.IsEmpty ? pixel : Rectangle.Union(redBounds, pixel);
                    }
                }

                if (redBounds.IsEmpty)
                    return maskCanvas;

                using (Bitmap artwork = new Bitmap(MaxIconSize, MaxIconSize, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(artwork))
                    {
                        ConfigureGraphics(g);
                        g.Clear(Color.Transparent);
                        Rectangle target = FillRectangle(frontDesign.Width, frontDesign.Height, redBounds);
                        g.DrawImage(frontDesign, target);
                    }

                    for (int y = 0; y < maskCanvas.Height; y++)
                    {
                        for (int x = 0; x < maskCanvas.Width; x++)
                        {
                            Color maskPixel = maskCanvas.GetPixel(x, y);
                            if (!IsBannerMaskRed(maskPixel))
                                continue;

                            Color artPixel = artwork.GetPixel(x, y);
                            int alpha = Math.Min(maskPixel.A, artPixel.A);
                            maskCanvas.SetPixel(x, y, Color.FromArgb(alpha, artPixel.R, artPixel.G, artPixel.B));
                        }
                    }
                }

                return maskCanvas;
            }
        }

        public static void DeletePreparedFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            try
            {
                string preparedRoot = Path.GetFullPath(Path.Combine(AppInfo.LocalDataRoot, "Prepared"));
                string full = Path.GetFullPath(path);
                if (full.StartsWith(preparedRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(full))
                    File.Delete(full);
            }
            catch { }
        }

        private static Bitmap CreateBannerDesignPreview(Image source, TemplateDefinition template, string fitMode, ImagePlacementState placement, int maxWidth, int maxHeight, Color guideColor)
        {
            if (source == null || template == null || template.VisibleWidth <= 0 || template.VisibleHeight <= 0 || maxWidth <= 0 || maxHeight <= 0)
                return null;

            bool fantasy = template != null && !string.IsNullOrEmpty(template.Key) && template.Key.IndexOf("Fantasy", StringComparison.OrdinalIgnoreCase) >= 0;
            int designWidth = Math.Max(1, template.VisibleWidth);
            int designHeight = Math.Max(1, template.VisibleHeight);

            using (Bitmap design = new Bitmap(designWidth, designHeight, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(design))
                {
                    ConfigureGraphics(g);
                    g.Clear(Color.Transparent);
                    Rectangle bounds = new Rectangle(0, 0, designWidth, designHeight);
                    using (GraphicsPath shape = CreateBannerPreviewPath(fantasy, new RectangleF(1f, 1f, Math.Max(1f, designWidth - 2f), Math.Max(1f, designHeight - 2f))))
                    {
                        GraphicsState state = g.Save();
                        g.SetClip(shape);
                        DrawImage(g, source, bounds, fitMode, placement);
                        g.Restore(state);

                        float outlineWidth = Math.Max(2.0f, Math.Min(4.0f, designWidth / 90.0f));
                        using (Pen outer = new Pen(Color.FromArgb(185, 0, 0, 0), outlineWidth + 2.0f))
                        using (Pen guide = new Pen(guideColor, outlineWidth))
                        {
                            outer.Alignment = PenAlignment.Inset;
                            guide.Alignment = PenAlignment.Inset;
                            g.DrawPath(outer, shape);
                            g.DrawPath(guide, shape);
                        }
                    }
                }

                double scale = Math.Min((double)maxWidth / design.Width, (double)maxHeight / design.Height);
                scale = Math.Min(1.0, scale);
                int width = Math.Max(1, (int)Math.Round(design.Width * scale));
                int height = Math.Max(1, (int)Math.Round(design.Height * scale));
                Bitmap preview = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(preview))
                {
                    ConfigureGraphics(g);
                    g.Clear(Color.Transparent);
                    g.DrawImage(design, new Rectangle(0, 0, width, height));
                }
                return preview;
            }
        }

        private static Bitmap CreateBannerDesignSurface(Image source, TemplateDefinition template, string fitMode, ImagePlacementState placement)
        {
            if (source == null || template == null || template.VisibleWidth <= 0 || template.VisibleHeight <= 0)
                return null;

            bool fantasy = template != null && !string.IsNullOrEmpty(template.Key) && template.Key.IndexOf("Fantasy", StringComparison.OrdinalIgnoreCase) >= 0;
            int designWidth = Math.Max(1, template.VisibleWidth);
            int designHeight = Math.Max(1, template.VisibleHeight);
            Bitmap design = new Bitmap(designWidth, designHeight, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(design))
            {
                ConfigureGraphics(g);
                g.Clear(Color.Transparent);
                Rectangle bounds = new Rectangle(0, 0, designWidth, designHeight);
                using (GraphicsPath shape = CreateBannerPreviewPath(fantasy, new RectangleF(1f, 1f, Math.Max(1f, designWidth - 2f), Math.Max(1f, designHeight - 2f))))
                {
                    GraphicsState state = g.Save();
                    g.SetClip(shape);
                    DrawImage(g, source, bounds, fitMode, placement);
                    g.Restore(state);
                }
            }
            return design;
        }

        private static GraphicsPath CreateBannerPreviewPath(bool fantasy, RectangleF bounds)
        {
            GraphicsPath path = new GraphicsPath();
            if (!fantasy)
            {
                path.AddRectangle(bounds);
                path.CloseFigure();
                return path;
            }

            // The supplied Fantasy UVW maps one full swallow-tail banner across two
            // adjacent UV halves. The centre point of the V sits about 12.3% of the
            // mapped height above the two lower outside corners.
            float notchY = bounds.Bottom - (bounds.Height * 0.123f);
            path.AddPolygon(new PointF[]
            {
                new PointF(bounds.Left, bounds.Top),
                new PointF(bounds.Right, bounds.Top),
                new PointF(bounds.Right, bounds.Bottom),
                new PointF(bounds.Left + (bounds.Width * 0.50f), notchY),
                new PointF(bounds.Left, bounds.Bottom)
            });
            path.CloseFigure();
            return path;
        }

        private static Bitmap RenderWallpaperTexture(string sourcePath, string fitMode, ImagePlacementState placement)
        {
            using (Bitmap source = LoadUnlockedBitmap(sourcePath))
                return RenderWallpaperTexture(source, fitMode, placement);
        }

        private static Bitmap RenderWallpaperTexture(Image source, string fitMode, ImagePlacementState placement)
        {
            if (source == null)
                throw new ArgumentNullException("source");

            // BL-022 Preview 10: wallpaper output is a fixed square authoring surface.
            // The imported source image is content inside the 1024 x 1024 texture; its own
            // aspect ratio must never redefine the final wallpaper texture dimensions.
            Bitmap output = new Bitmap(WallpaperTextureSize, WallpaperTextureSize, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(output))
            {
                ConfigureGraphics(g);
                g.Clear(Color.Transparent);
                DrawImage(g, source, new Rectangle(0, 0, WallpaperTextureSize, WallpaperTextureSize),
                    string.IsNullOrWhiteSpace(fitMode) ? "Fill" : fitMode, placement ?? new ImagePlacementState());
            }
            return output;
        }

        private static Bitmap CreateWallpaperPreview(Image source, string fitMode, ImagePlacementState placement, int maxWidth, int maxHeight, Color guideColor)
        {
            if (source == null || maxWidth <= 0 || maxHeight <= 0)
                return null;

            int previewSize = Math.Max(1, Math.Min(maxWidth, maxHeight));
            using (Bitmap rendered = RenderWallpaperTexture(source, fitMode, placement))
            {
                Bitmap preview = new Bitmap(previewSize, previewSize, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(preview))
                {
                    ConfigureInteractiveGraphics(g);
                    g.Clear(Color.FromArgb(36, 36, 36));
                    Rectangle square = new Rectangle(0, 0, previewSize, previewSize);
                    g.DrawImage(rendered, square);
                    DrawDimmedGuide(g, square, square, guideColor);
                }
                return preview;
            }
        }

        public Bitmap CreateWallpaperGuidePreview(int maxWidth, int maxHeight, Color guideColor)
        {
            if (maxWidth <= 0 || maxHeight <= 0)
                return null;

            int previewSize = Math.Max(1, Math.Min(maxWidth, maxHeight));
            Bitmap preview = new Bitmap(previewSize, previewSize, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(preview))
            {
                ConfigureInteractiveGraphics(g);
                Rectangle square = new Rectangle(0, 0, previewSize, previewSize);
                g.Clear(Color.FromArgb(36, 36, 36));
                DrawDimmedGuide(g, square, square, guideColor);
            }
            return preview;
        }

        private static Bitmap RenderTexture(string sourcePath, TemplateDefinition template, string fitMode, ImagePlacementState placement)
        {
            if (IsBannerTemplate(template))
                return RenderBannerTexture(sourcePath, template, fitMode, placement);

            if (template.TextureWidth <= 0 || template.TextureHeight <= 0 ||
                template.VisibleWidth <= 0 || template.VisibleHeight <= 0)
                throw new InvalidDataException("Template mapping dimensions are invalid for " + template.DisplayName + ".");

            using (Bitmap source = LoadUnlockedBitmap(sourcePath))
            {
                Bitmap output = new Bitmap(template.TextureWidth, template.TextureHeight, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(output))
                {
                    ConfigureGraphics(g);
                    g.Clear(Color.Black);
                    Rectangle target = new Rectangle(template.VisibleX, template.VisibleY, template.VisibleWidth, template.VisibleHeight);
                    if (IsPosterTemplate(template))
                    {
                        EnsureFamilyReferenceInternal(template, placement);
                        DrawReferencedImageClipped(g, source, target, fitMode, placement);
                    }
                    else
                    {
                        DrawImage(g, source, target, fitMode, placement);
                    }
                }
                return output;
            }
        }

        private static Bitmap RenderBannerTexture(string sourcePath, TemplateDefinition template, string fitMode, ImagePlacementState placement)
        {
            if (template == null || template.TextureWidth <= 0 || template.TextureHeight <= 0 ||
                template.VisibleWidth <= 0 || template.VisibleHeight <= 0)
                throw new InvalidDataException("Banner mapping dimensions are invalid for " + (template == null ? "this banner" : template.DisplayName) + ".");

            using (Bitmap source = LoadUnlockedBitmap(sourcePath))
            {
                Bitmap output = new Bitmap(template.TextureWidth, template.TextureHeight, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(output))
                {
                    ConfigureGraphics(g);
                    g.Clear(Color.Black);

                    Rectangle primary = new Rectangle(template.VisibleX, template.VisibleY, template.VisibleWidth, template.VisibleHeight);
                    DrawImage(g, source, primary, fitMode, placement);

                    // Fantasy's supplied UVW splits one physical artwork face into two
                    // adjacent halves inside this single combined rectangle. The source must be
                    // composed once across the full span; duplicating it per half creates two
                    // squeezed copies and does not match the mesh mapping.
                }
                return output;
            }
        }

        private static Bitmap RenderRugDesignShape(string sourcePath, TemplateDefinition template, string fitMode, ImagePlacementState placement)
        {
            RugShapeProfile profile = GetRugShapeProfile(template == null ? null : template.Key);
            if (profile == null)
                throw new InvalidDataException("No rug mapping profile was found for " + (template == null ? "this rug" : template.DisplayName) + ".");

            using (Bitmap source = LoadUnlockedBitmap(sourcePath))
                return RenderRugDesignShape(source, profile, fitMode, placement);
        }

        private static Bitmap RenderRugDesignShape(Image source, RugShapeProfile profile, string fitMode, ImagePlacementState placement)
        {
            int width = Math.Max(1, (int)Math.Round(profile.ShapeWidth));
            int height = Math.Max(1, (int)Math.Round(profile.ShapeHeight));
            Bitmap output = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(output))
            using (GraphicsPath shape = CreateRugShapePath(profile.ShapeKind, new RectangleF(1f, 1f, width - 2f, height - 2f)))
            {
                ConfigureGraphics(g);
                g.Clear(Color.Transparent);
                GraphicsState state = g.Save();
                g.SetClip(shape);
                DrawReferencedImageWithoutClip(g, source, new Rectangle(0, 0, width, height), fitMode, placement);
                g.Restore(state);
            }
            return output;
        }

        private static Rectangle GetRectangleRugArtworkUvTarget(string templateKey, int textureSize)
        {
            bool largeRectangle = !string.IsNullOrEmpty(templateKey) &&
                templateKey.IndexOf("Large", StringComparison.OrdinalIgnoreCase) >= 0;

            // Bounds measured from the refreshed Rectangle replacement mesh top-surface
            // artwork UVs. Side/corner UVs are collapsed/degenerate and intentionally do
            // not participate in this visible-surface mapping.
            float minX = largeRectangle ? 0.00012439f : 0.18452731f;
            float minY = largeRectangle ? 0.19624990f : 0.33339232f;
            float maxX = largeRectangle ? 0.99987680f : 0.81568801f;
            float maxY = largeRectangle ? 0.80358744f : 0.66629976f;

            int x = Math.Max(0, Math.Min(textureSize - 1, (int)Math.Round(minX * textureSize)));
            int y = Math.Max(0, Math.Min(textureSize - 1, (int)Math.Round(minY * textureSize)));
            int right = Math.Max(x + 1, Math.Min(textureSize, (int)Math.Round(maxX * textureSize)));
            int bottom = Math.Max(y + 1, Math.Min(textureSize, (int)Math.Round(maxY * textureSize)));
            return Rectangle.FromLTRB(x, y, right, bottom);
        }

        private static void DrawImageWithoutClip(Graphics g, Image source, Rectangle target, string fitMode, ImagePlacementState placement)
        {
            float zoom = placement == null || placement.Zoom <= 0 ? 1.0f : placement.Zoom;
            float offsetX = placement == null ? 0.0f : placement.OffsetX;
            float offsetY = placement == null ? 0.0f : placement.OffsetY;
            string mode = (fitMode ?? "Fill").Trim();

            int width;
            int height;
            if (string.Equals(mode, "Stretch", StringComparison.OrdinalIgnoreCase))
            {
                width = Math.Max(1, (int)Math.Round(target.Width * zoom));
                height = Math.Max(1, (int)Math.Round(target.Height * zoom));
            }
            else
            {
                double sx = (double)target.Width / source.Width;
                double sy = (double)target.Height / source.Height;
                bool fit = string.Equals(mode, "Fit", StringComparison.OrdinalIgnoreCase);
                double baseScale = fit ? Math.Min(sx, sy) : Math.Max(sx, sy);
                width = Math.Max(1, (int)Math.Round(source.Width * baseScale * zoom));
                height = Math.Max(1, (int)Math.Round(source.Height * baseScale * zoom));
            }

            int x = target.X + (target.Width - width) / 2 + (int)Math.Round(offsetX);
            int y = target.Y + (target.Height - height) / 2 + (int)Math.Round(offsetY);
            float rotationDegrees = placement == null ? 0.0f : placement.RotationDegrees;

            GraphicsState state = g.Save();
            if (Math.Abs(rotationDegrees) > 0.0001f)
            {
                float centreX = x + width / 2.0f;
                float centreY = y + height / 2.0f;
                g.TranslateTransform(centreX, centreY);
                g.RotateTransform(rotationDegrees);
                g.TranslateTransform(-centreX, -centreY);
            }
            g.DrawImage(source, new Rectangle(x, y, width, height));
            g.Restore(state);
        }

        private static Bitmap RenderContinuousRectangleRugAtlas(
            Image source,
            RugShapeProfile profile,
            Rectangle uvTarget,
            string fitMode,
            ImagePlacementState placement,
            Color fallback,
            int textureSize)
        {
            float longest = Math.Max(profile.ShapeWidth, profile.ShapeHeight);
            float scale = longest <= 0f ? 1f : Math.Max(1f, textureSize / longest);
            int designWidth = Math.Max(1, (int)Math.Round(profile.ShapeWidth * scale));
            int designHeight = Math.Max(1, (int)Math.Round(profile.ShapeHeight * scale));
            ImagePlacementState renderPlacement = ScalePlacement(placement, scale);

            // Work in the same coordinate system as the editor composition, but make that
            // canvas large enough to represent the complete 1024 x 1024 atlas. The normal
            // design viewport is positioned so that it maps exactly onto the mesh's real
            // top-face UV rectangle. Drawing without the normal viewport clip allows the
            // source image to continue naturally above and below the visible rug surface.
            double designPerTextureX = (double)designWidth / Math.Max(1, uvTarget.Width);
            double designPerTextureY = (double)designHeight / Math.Max(1, uvTarget.Height);

            int leftMargin = Math.Max(0, (int)Math.Round(uvTarget.Left * designPerTextureX));
            int topMargin = Math.Max(0, (int)Math.Round(uvTarget.Top * designPerTextureY));
            int rightMargin = Math.Max(0, (int)Math.Round((textureSize - uvTarget.Right) * designPerTextureX));
            int bottomMargin = Math.Max(0, (int)Math.Round((textureSize - uvTarget.Bottom) * designPerTextureY));

            int expandedWidth = Math.Max(1, leftMargin + designWidth + rightMargin);
            int expandedHeight = Math.Max(1, topMargin + designHeight + bottomMargin);
            Rectangle designViewport = new Rectangle(leftMargin, topMargin, designWidth, designHeight);

            using (Bitmap expanded = new Bitmap(expandedWidth, expandedHeight, PixelFormat.Format24bppRgb))
            {
                using (Graphics g = Graphics.FromImage(expanded))
                {
                    ConfigureGraphics(g);
                    g.Clear(fallback);

                    // Preserve the existing opaque-background behaviour, but render the
                    // underlying image continuously instead of stretching one edge row.
                    DrawImageWithoutClip(g, source, designViewport, "Fill", new ImagePlacementState { Zoom = 1.0f });
                    DrawReferencedImageWithoutClip(g, source, designViewport, fitMode, renderPlacement);
                }

                Bitmap output = new Bitmap(textureSize, textureSize, PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(output))
                {
                    ConfigureGraphics(g);
                    g.Clear(fallback);
                    g.DrawImage(expanded, new Rectangle(0, 0, textureSize, textureSize));
                }
                return output;
            }
        }

        private static Bitmap RenderReplacementRugDesignTexture(string sourcePath, TemplateDefinition template, string fitMode, ImagePlacementState placement)
        {
            RugShapeProfile profile = GetRugShapeProfile(template == null ? null : template.Key);
            if (profile == null)
                throw new InvalidDataException("No rug mapping profile was found for " + (template == null ? "this rug" : template.DisplayName) + ".");

            const int textureSize = 1024;
            float longest = Math.Max(profile.ShapeWidth, profile.ShapeHeight);
            float scale = longest <= 0f ? 1f : Math.Max(1f, textureSize / longest);
            int designWidth = Math.Max(1, (int)Math.Round(profile.ShapeWidth * scale));
            int designHeight = Math.Max(1, (int)Math.Round(profile.ShapeHeight * scale));
            ImagePlacementState renderPlacement = ScalePlacement(placement, scale);

            using (Bitmap source = LoadUnlockedBitmap(sourcePath))
            {
                Color fallback = GetAverageOpaqueColor(source);

                if (string.Equals(profile.ShapeKind, "Rectangle", StringComparison.OrdinalIgnoreCase))
                {
                    Rectangle uvTarget = GetRectangleRugArtworkUvTarget(template == null ? null : template.Key, textureSize);
                    return RenderContinuousRectangleRugAtlas(source, profile, uvTarget, fitMode, placement, fallback, textureSize);
                }

                // Non-rectangle rugs retain the established full-atlas behaviour.
                using (Bitmap designCanvas = new Bitmap(designWidth, designHeight, PixelFormat.Format24bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(designCanvas))
                    {
                        ConfigureGraphics(g);
                        g.Clear(fallback);
                        DrawImage(g, source, new Rectangle(0, 0, designWidth, designHeight), "Fill", new ImagePlacementState { Zoom = 1.0f });
                        DrawImage(g, source, new Rectangle(0, 0, designWidth, designHeight), fitMode, renderPlacement);
                    }

                    Bitmap output = new Bitmap(textureSize, textureSize, PixelFormat.Format24bppRgb);
                    using (Graphics g = Graphics.FromImage(output))
                    {
                        ConfigureGraphics(g);
                        g.DrawImage(designCanvas, new Rectangle(0, 0, textureSize, textureSize));
                    }
                    return output;
                }
            }
        }

        private static RugShapeProfile ScaleRugShapeProfile(RugShapeProfile source, float scale)
        {
            RugShapeProfile result = new RugShapeProfile();
            result.ShapeKind = source.ShapeKind;
            result.ShapeWidth = source.ShapeWidth * scale;
            result.ShapeHeight = source.ShapeHeight * scale;
            return result;
        }

private static Bitmap RenderIconForBuild(string originalArtworkPath, string preparedArtworkPath, string customIconPath, TemplateDefinition template, string fitMode, ImagePlacementState placement, string iconMode)
        {
            string mode = NormalizeIconMode(iconMode);
            if (string.Equals(mode, "Custom Icon File", StringComparison.OrdinalIgnoreCase))
            {
                using (Bitmap source = LoadUnlockedBitmap(customIconPath))
                    return RenderBasicSquareIcon(source);
            }

            if (string.Equals(mode, "Original Artwork", StringComparison.OrdinalIgnoreCase))
            {
                using (Bitmap source = LoadUnlockedBitmap(originalArtworkPath))
                    return RenderBasicSquareIcon(source);
            }

            if (string.Equals(mode, "Final Texture", StringComparison.OrdinalIgnoreCase))
            {
                using (Bitmap source = LoadUnlockedBitmap(preparedArtworkPath))
                    return RenderBasicSquareIcon(source);
            }

            // Wallpaper Generated icons must reflect the final authored 1024 x 1024
            // composition (including Fill/Fit/Stretch, pan, zoom and rotation), rather than
            // reverting to the source image's original aspect ratio.
            if (IsWallpaperTemplate(template))
            {
                using (Bitmap source = LoadUnlockedBitmap(preparedArtworkPath))
                    return RenderBasicSquareIcon(source);
            }

            // Other non-image-processing room visuals use their source image directly rather
            // than one of Memento Maker's mesh/UV texture-layout processors.
            if (template == null || !template.ImageProcessingEnabled)
            {
                using (Bitmap source = LoadUnlockedBitmap(originalArtworkPath))
                    return RenderBasicSquareIcon(source);
            }

            if (IsRugTemplate(template))
            {
                // For rug Template Styled icons, use the clean rug-shape design preview rather than
                // the baked texture tile. This keeps the icon aligned with what the user composed.
                using (Bitmap designShape = RenderRugDesignShape(originalArtworkPath, template, fitMode, placement))
                    return RenderRugStyledIconFromShape(designShape, template);
            }

            if (IsBannerTemplate(template))
            {
                using (Bitmap source = LoadUnlockedBitmap(originalArtworkPath))
                using (Bitmap design = CreateBannerDesignSurface(source, template, fitMode, placement))
                    return RenderTemplateStyledIcon(design, template);
            }

            using (Bitmap source = LoadUnlockedBitmap(preparedArtworkPath))
            using (Bitmap visible = ExtractVisibleRegion(source, template))
                return RenderTemplateStyledIcon(visible, template);
        }

private static Bitmap RenderIconFromSourcePaths(string artworkPath, string customIconPath, TemplateDefinition template, string fitMode, ImagePlacementState placement, string iconMode)
        {
            string mode = NormalizeIconMode(iconMode);
            if (string.Equals(mode, "Custom Icon File", StringComparison.OrdinalIgnoreCase))
            {
                using (Bitmap source = LoadUnlockedBitmap(customIconPath))
                    return RenderBasicSquareIcon(source);
            }

            if (string.Equals(mode, "Original Artwork", StringComparison.OrdinalIgnoreCase) || template == null || !template.ImageProcessingEnabled)
            {
                using (Bitmap source = LoadUnlockedBitmap(artworkPath))
                    return RenderBasicSquareIcon(source);
            }

            // Keep the live Generated icon preview in lock-step with the same fixed square
            // Wallpaper composition that will be written at build time.
            if (IsWallpaperTemplate(template))
            {
                using (Bitmap wallpaper = RenderWallpaperTexture(artworkPath, fitMode, placement))
                    return RenderBasicSquareIcon(wallpaper);
            }

            if (IsRugTemplate(template))
            {
                if (string.Equals(mode, "Final Texture", StringComparison.OrdinalIgnoreCase))
                {
                    using (Bitmap preparedDesign = RenderReplacementRugDesignTexture(artworkPath, template, fitMode, placement))
                        return RenderBasicSquareIcon(preparedDesign);
                }

                using (Bitmap designShape = RenderRugDesignShape(artworkPath, template, fitMode, placement))
                    return RenderRugStyledIconFromShape(designShape, template);
            }

            if (IsBannerTemplate(template) && !string.Equals(mode, "Final Texture", StringComparison.OrdinalIgnoreCase))
            {
                using (Bitmap source = LoadUnlockedBitmap(artworkPath))
                using (Bitmap design = CreateBannerDesignSurface(source, template, fitMode, placement))
                    return RenderTemplateStyledIcon(design, template);
            }

            using (Bitmap processed = RenderTexture(artworkPath, template, fitMode, placement))
            {
                if (string.Equals(mode, "Final Texture", StringComparison.OrdinalIgnoreCase))
                    return RenderBasicSquareIcon(processed);

                using (Bitmap visible = ExtractVisibleRegion(processed, template))
                    return RenderTemplateStyledIcon(visible, template);
            }
        }

private static string NormalizeIconMode(string iconMode)
        {
            return string.IsNullOrEmpty(iconMode) ? "Original Artwork" : iconMode.Trim();
        }

        private static Bitmap RenderTemplateStyledIcon(Bitmap source, TemplateDefinition template)
        {
            string key = template == null ? string.Empty : (template.Key ?? string.Empty);
            if (IsPosterTemplate(template))
                return RenderPosterMaskedIcon(source, key);
            if (string.Equals(key, "Mural", StringComparison.OrdinalIgnoreCase))
                return RenderMuralStyledIcon(source);
            if (key.IndexOf("Rug", StringComparison.OrdinalIgnoreCase) >= 0)
                return RenderRugStyledIconFromShape(source, template);
            if (key.EndsWith(" Banner", StringComparison.OrdinalIgnoreCase))
                return RenderBannerStyledIcon(source, string.Equals(key, "Fantasy Banner", StringComparison.OrdinalIgnoreCase));
            return RenderBasicSquareIcon(source);
        }

        private static Bitmap RenderBasicSquareIcon(Image source)
        {
            Bitmap output = new Bitmap(MaxIconSize, MaxIconSize, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(output))
            {
                ConfigureGraphics(g);
                g.Clear(Color.Transparent);
                Rectangle target = FitRectangle(source.Width, source.Height, new Rectangle(0, 0, MaxIconSize, MaxIconSize), false);
                g.DrawImage(source, target);
            }
            return output;
        }

        private static Bitmap RenderBannerStyledIcon(Image source, bool fantasy)
        {
            string maskFile = fantasy ? "banner_fantasy_icon_mask.png" : "banner_general_icon_mask.png";
            string maskPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Theme", maskFile);
            if (!File.Exists(maskPath))
                return RenderBasicSquareIcon(source);

            using (Bitmap rawMask = LoadUnlockedBitmap(maskPath))
            {
                Bitmap maskCanvas = new Bitmap(MaxIconSize, MaxIconSize, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(maskCanvas))
                {
                    ConfigureGraphics(g);
                    g.Clear(Color.Transparent);
                    Rectangle fitted = FitRectangle(rawMask.Width, rawMask.Height,
                        new Rectangle(4, 4, MaxIconSize - 8, MaxIconSize - 8), false);
                    g.DrawImage(rawMask, fitted);
                }

                Rectangle redBounds = Rectangle.Empty;
                for (int y = 0; y < maskCanvas.Height; y++)
                {
                    for (int x = 0; x < maskCanvas.Width; x++)
                    {
                        Color c = maskCanvas.GetPixel(x, y);
                        if (!IsBannerMaskRed(c))
                            continue;
                        if (redBounds.IsEmpty)
                            redBounds = new Rectangle(x, y, 1, 1);
                        else
                            redBounds = Rectangle.Union(redBounds, new Rectangle(x, y, 1, 1));
                    }
                }

                if (redBounds.IsEmpty)
                    return maskCanvas;

                Bitmap artwork = new Bitmap(MaxIconSize, MaxIconSize, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(artwork))
                {
                    ConfigureGraphics(g);
                    g.Clear(Color.Transparent);
                    Rectangle target = FillRectangle(source.Width, source.Height, redBounds);
                    g.DrawImage(source, target);
                }

                for (int y = 0; y < maskCanvas.Height; y++)
                {
                    for (int x = 0; x < maskCanvas.Width; x++)
                    {
                        Color maskPixel = maskCanvas.GetPixel(x, y);
                        if (!IsBannerMaskRed(maskPixel))
                            continue;

                        Color artPixel = artwork.GetPixel(x, y);
                        // Preserve the mask alpha at feathered edges while replacing only the
                        // deliberately red artwork placeholder in the supplied gold-standard mask.
                        int alpha = Math.Min(maskPixel.A, artPixel.A);
                        maskCanvas.SetPixel(x, y, Color.FromArgb(alpha, artPixel.R, artPixel.G, artPixel.B));
                    }
                }

                artwork.Dispose();
                return maskCanvas;
            }
        }

        private static bool IsBannerMaskRed(Color color)
        {
            return color.A > 20 && color.R >= 180 && color.G <= 100 && color.B <= 100 &&
                color.R > color.G + 70 && color.R > color.B + 70;
        }

        private static Bitmap RenderPosterMaskedIcon(Image source, string templateKey)
        {
            string maskFile = GetPosterIconMaskFile(templateKey);
            string maskPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Theme", maskFile);
            if (!File.Exists(maskPath))
                return RenderPosterStyledIcon(source);

            using (Bitmap rawMask = LoadUnlockedBitmap(maskPath))
            {
                Bitmap maskCanvas = new Bitmap(MaxIconSize, MaxIconSize, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(maskCanvas))
                {
                    ConfigureGraphics(g);
                    g.Clear(Color.Transparent);
                    g.DrawImage(rawMask, new Rectangle(0, 0, MaxIconSize, MaxIconSize));
                }

                Rectangle redBounds = Rectangle.Empty;
                for (int y = 0; y < maskCanvas.Height; y++)
                {
                    for (int x = 0; x < maskCanvas.Width; x++)
                    {
                        Color c = maskCanvas.GetPixel(x, y);
                        if (!IsBannerMaskRed(c))
                            continue;
                        Rectangle pixel = new Rectangle(x, y, 1, 1);
                        redBounds = redBounds.IsEmpty ? pixel : Rectangle.Union(redBounds, pixel);
                    }
                }

                if (redBounds.IsEmpty)
                    return maskCanvas;

                using (Bitmap artwork = new Bitmap(MaxIconSize, MaxIconSize, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(artwork))
                    {
                        ConfigureGraphics(g);
                        g.Clear(Color.Transparent);
                        Rectangle target = FillRectangle(source.Width, source.Height, redBounds);
                        g.DrawImage(source, target);
                    }

                    for (int y = 0; y < maskCanvas.Height; y++)
                    {
                        for (int x = 0; x < maskCanvas.Width; x++)
                        {
                            Color maskPixel = maskCanvas.GetPixel(x, y);
                            if (!IsBannerMaskRed(maskPixel))
                                continue;

                            Color artPixel = artwork.GetPixel(x, y);
                            int alpha = Math.Min(maskPixel.A, artPixel.A);
                            maskCanvas.SetPixel(x, y, Color.FromArgb(alpha, artPixel.R, artPixel.G, artPixel.B));
                        }
                    }
                }

                return maskCanvas;
            }
        }

        private static string GetPosterIconMaskFile(string templateKey)
        {
            if (!string.IsNullOrEmpty(templateKey) && templateKey.StartsWith("Small ", StringComparison.OrdinalIgnoreCase))
                return "poster_small_icon_mask.png";
            if (!string.IsNullOrEmpty(templateKey) && templateKey.StartsWith("Tall ", StringComparison.OrdinalIgnoreCase))
                return "poster_tall_icon_mask.png";
            return "poster_standard_icon_mask.png";
        }

        private static Bitmap RenderPosterStyledIcon(Image source)
        {
            Bitmap output = new Bitmap(MaxIconSize, MaxIconSize, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(output))
            {
                ConfigureGraphics(g);
                g.Clear(Color.Transparent);

                Rectangle shadowRect = new Rectangle(51, 12, 160, 236);
                Rectangle outerRect = new Rectangle(46, 8, 160, 236);
                Rectangle innerRect = new Rectangle(60, 22, 132, 208);

                using (GraphicsPath shadowPath = CreateRoundedRectangle(shadowRect, 18))
                using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
                {
                    g.FillPath(shadowBrush, shadowPath);
                }

                using (GraphicsPath outerPath = CreateRoundedRectangle(outerRect, 18))
                // Poster icon polish: lighten the original charcoal surround by blending it
                // 10% toward white while preserving the established frame tone and geometry.
                using (SolidBrush frameBrush = new SolidBrush(Color.FromArgb(47, 58, 59)))
                using (Pen borderPen = new Pen(Color.Black, 3f))
                {
                    g.FillPath(frameBrush, outerPath);
                    g.DrawPath(borderPen, outerPath);
                }

                using (GraphicsPath innerPath = CreateRoundedRectangle(innerRect, 12))
                {
                    GraphicsState state = g.Save();
                    g.SetClip(innerPath);
                    Rectangle target = FillRectangle(source.Width, source.Height, innerRect);
                    g.DrawImage(source, target);
                    g.Restore(state);

                    // Deliberately no inner stroke: the previous semi-opaque white pen
                    // produced a visible halo around the poster artwork that is not present
                    // on the in-game item.
                }

                // Deliberately no top highlight line. The previous white highlight rendered
                // as an unintended bar across the upper part of the generated poster frame.
            }
            return output;
        }

        private static Bitmap RenderMuralStyledIcon(Image source)
        {
            string maskPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Theme", "Mural_Icon_Shape.png");
            if (!File.Exists(maskPath))
                return RenderMuralPerspectiveFallback(source);

            using (Bitmap rawMask = LoadUnlockedBitmap(maskPath))
            {
                Bitmap maskCanvas = new Bitmap(MaxIconSize, MaxIconSize, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(maskCanvas))
                {
                    ConfigureGraphics(g);
                    g.Clear(Color.Transparent);
                    g.DrawImage(rawMask, new Rectangle(0, 0, MaxIconSize, MaxIconSize));
                }

                // Deliberately mirror RenderWallSignMaskedIcon: the supplied red mask is only
                // the chroma placeholder. The actual visible mural face is a measured quad
                // inside that placeholder, and the generated mural texture is sampled directly
                // through the inverse projective transform. Do not pre-fit, letterbox or apply a
                // second inner artwork transform; those extra stages were the cause of the
                // incorrect mapping in the earlier mural preview fixes.
                Rectangle redBounds = Rectangle.Empty;
                for (int y = 0; y < maskCanvas.Height; y++)
                {
                    for (int x = 0; x < maskCanvas.Width; x++)
                    {
                        Color c = maskCanvas.GetPixel(x, y);
                        if (!IsBannerMaskRed(c))
                            continue;

                        Rectangle pixel = new Rectangle(x, y, 1, 1);
                        redBounds = redBounds.IsEmpty ? pixel : Rectangle.Union(redBounds, pixel);
                    }
                }

                if (redBounds.IsEmpty)
                    return maskCanvas;

                PointF topLeft;
                PointF topRight;
                PointF bottomRight;
                PointF bottomLeft;
                GetMuralIconFaceQuad(redBounds, out topLeft, out topRight, out bottomRight, out bottomLeft);

                double[] inverse = BuildInverseUnitSquareToQuadTransform(topLeft, topRight, bottomRight, bottomLeft);
                if (inverse == null)
                    return maskCanvas;

                for (int y = 0; y < maskCanvas.Height; y++)
                {
                    for (int x = 0; x < maskCanvas.Width; x++)
                    {
                        Color maskPixel = maskCanvas.GetPixel(x, y);
                        if (!IsBannerMaskRed(maskPixel))
                            continue;

                        double denominator = (inverse[6] * (x + 0.5)) +
                            (inverse[7] * (y + 0.5)) + inverse[8];
                        if (Math.Abs(denominator) < 0.0000001)
                        {
                            maskCanvas.SetPixel(x, y, Color.Transparent);
                            continue;
                        }

                        double u = ((inverse[0] * (x + 0.5)) +
                            (inverse[1] * (y + 0.5)) + inverse[2]) / denominator;
                        double v = ((inverse[3] * (x + 0.5)) +
                            (inverse[4] * (y + 0.5)) + inverse[5]) / denominator;
                        if (u < 0.0 || u > 1.0 || v < 0.0 || v > 1.0)
                        {
                            maskCanvas.SetPixel(x, y, Color.Transparent);
                            continue;
                        }

                        Color artPixel = SampleBilinear(source,
                            u * Math.Max(0, source.Width - 1),
                            v * Math.Max(0, source.Height - 1));
                        int alpha = Math.Min(maskPixel.A, artPixel.A);
                        maskCanvas.SetPixel(x, y, Color.FromArgb(alpha, artPixel.R, artPixel.G, artPixel.B));
                    }
                }

                return maskCanvas;
            }
        }

        private static void GetMuralIconFaceQuad(Rectangle redBounds,
            out PointF topLeft, out PointF topRight, out PointF bottomRight, out PointF bottomLeft)
        {
            // Measured against the supplied 256 x 256 mural mask. Keep the accepted Fix 5
            // silhouette, but make the far/right edge slightly shorter than the near/left edge,
            // just as the Wall Sign face quad does. This is the only perspective geometry used
            // for the mural; the artwork and silhouette therefore remain perfectly aligned.
            float left = redBounds.Left;
            float right = Math.Max(redBounds.Left, redBounds.Right - 1);
            float top = redBounds.Top;
            float bottom = Math.Max(redBounds.Top, redBounds.Bottom - 1);
            float height = Math.Max(1f, bottom - top);

            // More noticeable perspective tuning pass: keep the same Wall Sign-style mapping
            // logic, but push the near/far height difference further so the mural text reads
            // with a clearer receding perspective in the icon preview.
            float topLeftDrop = Math.Max(1f, height * 0.23f);
            float leftFaceHeight = Math.Max(1f, height - topLeftDrop);
            float rightFaceHeight = Math.Max(1f, leftFaceHeight * 0.80f);

            topLeft = new PointF(left, Math.Min(bottom, top + topLeftDrop));
            topRight = new PointF(right, top);
            bottomRight = new PointF(right, Math.Min(bottom, top + rightFaceHeight));
            bottomLeft = new PointF(left, bottom);
        }

        private static Bitmap RenderMuralPerspectiveFallback(Image source)
        {
            Bitmap output = new Bitmap(MaxIconSize, MaxIconSize, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(output))
            {
                ConfigureGraphics(g);
                g.Clear(Color.Transparent);

                // Legacy approved front-face fallback used only if the Theme mask cannot be
                // loaded or contains no chroma artwork region.
                Point frontTopLeft = new Point(14, 94);
                Point frontTopRight = new Point(239, 78);
                Point frontBottomLeft = new Point(14, 150);
                Point frontBottomRight = new Point(
                    frontTopRight.X + (frontBottomLeft.X - frontTopLeft.X),
                    frontTopRight.Y + (frontBottomLeft.Y - frontTopLeft.Y));

                using (GraphicsPath frontPath = new GraphicsPath())
                {
                    Point[] frontFace = new[] { frontTopLeft, frontTopRight, frontBottomRight, frontBottomLeft };
                    frontPath.AddPolygon(frontFace);
                    GraphicsState state = g.Save();
                    g.SetClip(frontPath);
                    g.DrawImage(source, new[] { frontTopLeft, frontTopRight, frontBottomLeft });
                    g.Restore(state);
                }
            }
            return output;
        }

        private static Color GetAverageOpaqueColor(Bitmap source)
        {
            if (source == null || source.Width <= 0 || source.Height <= 0)
                return Color.White;

            long r = 0;
            long g = 0;
            long b = 0;
            long count = 0;
            int stepX = Math.Max(1, source.Width / 64);
            int stepY = Math.Max(1, source.Height / 64);

            for (int y = 0; y < source.Height; y += stepY)
            {
                for (int x = 0; x < source.Width; x += stepX)
                {
                    Color pixel = source.GetPixel(x, y);
                    if (pixel.A < 16)
                        continue;
                    r += pixel.R;
                    g += pixel.G;
                    b += pixel.B;
                    count++;
                }
            }

            if (count == 0)
                return Color.White;

            return Color.FromArgb(
                (int)Math.Max(0, Math.Min(255, r / count)),
                (int)Math.Max(0, Math.Min(255, g / count)),
                (int)Math.Max(0, Math.Min(255, b / count)));
        }

private static Bitmap RenderRugStyledIconFromShape(Bitmap mappedShape, TemplateDefinition template)
        {
            string templateKey = template == null ? string.Empty : (template.Key ?? string.Empty);
            Bitmap output = new Bitmap(MaxIconSize, MaxIconSize, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(output))
            {
                ConfigureGraphics(g);
                g.Clear(Color.Transparent);

                RugShapeProfile profile = GetRugShapeProfile(templateKey);
                string shapeKind = profile == null ? GetRugShapeKind(templateKey) : profile.ShapeKind;
                Rectangle targetBounds = GetRugIconBounds(shapeKind);

                using (GraphicsPath shadowPath = CreateRugShapePath(shapeKind, new RectangleF(targetBounds.X + 4, targetBounds.Y + 5, targetBounds.Width, targetBounds.Height)))
                using (SolidBrush shadow = new SolidBrush(Color.FromArgb(65, 0, 0, 0)))
                    g.FillPath(shadow, shadowPath);

                Rectangle fitted = FitRectangle(mappedShape.Width, mappedShape.Height, targetBounds, true);
                using (GraphicsPath edgePath = CreateRugShapePath(shapeKind, new RectangleF(fitted.X, fitted.Y, fitted.Width, fitted.Height)))
                {
                    GraphicsState imageState = g.Save();
                    g.SetClip(edgePath);
                    g.DrawImage(mappedShape, fitted);
                    g.Restore(imageState);

                    using (Pen edge = new Pen(Color.FromArgb(32, 43, 44), 4f))
                        g.DrawPath(edge, edgePath);
                }
            }
            return output;
        }

        private static Rectangle GetRugIconBounds(string shapeKind)
        {
            if (string.Equals(shapeKind, "Rectangle", StringComparison.OrdinalIgnoreCase))
                return new Rectangle(16, 68, 224, 120);
            return new Rectangle(26, 26, 204, 204);
        }

        private static bool IsWallpaperTemplate(TemplateDefinition template)
        {
            return template != null && string.Equals(template.Key, "Wallpaper", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPosterTemplate(TemplateDefinition template)
        {
            if (template == null || string.IsNullOrEmpty(template.Key))
                return false;
            string key = template.Key;
            return string.Equals(key, "Poster", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Small Poster", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Standard Poster", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Tall Poster", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRugTemplate(TemplateDefinition template)
        {
            return template != null && !string.IsNullOrEmpty(template.Key) && template.Key.IndexOf("Rug", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsBannerTemplate(TemplateDefinition template)
        {
            return template != null && !string.IsNullOrEmpty(template.Key) &&
                template.Key.EndsWith(" Banner", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetRugShapeKind(string templateKey)
        {
            if (string.IsNullOrEmpty(templateKey))
                return "Square";
            if (templateKey.IndexOf("Circle", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Circle";
            if (templateKey.IndexOf("Rectangle", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Rectangle";
            if (templateKey.IndexOf("Octagon", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Octagon";
            return "Square";
        }

        private static RugShapeProfile GetRugShapeProfile(string templateKey)
        {
            if (string.IsNullOrEmpty(templateKey))
                return null;

            string shape = GetRugShapeKind(templateKey);
            bool large = templateKey.IndexOf("Large", StringComparison.OrdinalIgnoreCase) >= 0;

            // Guide dimensions are measured from the refreshed replacement rug mesh
            // footprints, then scaled onto the shared rug-family editor canvas. This keeps
            // one consistent rug artwork composition scale while making the Small guides
            // visibly smaller than the Large guides in proportion to the new meshes.
            const float guideScale = 500f / 2.58789086f; // Largest refreshed footprint.

            if (string.Equals(shape, "Rectangle", StringComparison.OrdinalIgnoreCase))
            {
                RugShapeProfile measured = large
                    ? NewRugShapeProfile("Rectangle", 2.57617211f, 1.54589880f)
                    : NewRugShapeProfile("Rectangle", 1.62792981f, 0.83740252f);
                return ScaleRugShapeProfile(measured, guideScale);
            }

            if (string.Equals(shape, "Circle", StringComparison.OrdinalIgnoreCase))
            {
                float extent = large ? 2.55859375f : 1.59765625f;
                return ScaleRugShapeProfile(NewRugShapeProfile("Circle", extent, extent), guideScale);
            }

            if (string.Equals(shape, "Octagon", StringComparison.OrdinalIgnoreCase))
            {
                float extent = large ? 2.58007813f : 1.47656238f;
                return ScaleRugShapeProfile(NewRugShapeProfile("Octagon", extent, extent), guideScale);
            }

            // Square guides remain square even though the imported Large square mesh has a
            // tiny asymmetry in its measured bounds.
            float squareExtent = large ? 2.58789086f : 1.62890649f;
            return ScaleRugShapeProfile(NewRugShapeProfile("Square", squareExtent, squareExtent), guideScale);
        }

        private static RugShapeProfile NewRugShapeProfile(string shapeKind, float shapeWidth, float shapeHeight)
        {
            RugShapeProfile profile = new RugShapeProfile();
            profile.ShapeKind = shapeKind;
            profile.ShapeWidth = shapeWidth;
            profile.ShapeHeight = shapeHeight;
            return profile;
        }

        private static GraphicsPath CreateRugShapePath(string shapeKind, RectangleF bounds)
        {
            if (string.Equals(shapeKind, "Circle", StringComparison.OrdinalIgnoreCase))
            {
                GraphicsPath circle = new GraphicsPath();
                circle.AddEllipse(bounds);
                return circle;
            }

            if (string.Equals(shapeKind, "Octagon", StringComparison.OrdinalIgnoreCase))
            {
                float inset = Math.Min(bounds.Width, bounds.Height) * 0.27f;
                GraphicsPath octagon = new GraphicsPath();
                octagon.AddPolygon(new PointF[]
                {
                    new PointF(bounds.Left + inset, bounds.Top),
                    new PointF(bounds.Right - inset, bounds.Top),
                    new PointF(bounds.Right, bounds.Top + inset),
                    new PointF(bounds.Right, bounds.Bottom - inset),
                    new PointF(bounds.Right - inset, bounds.Bottom),
                    new PointF(bounds.Left + inset, bounds.Bottom),
                    new PointF(bounds.Left, bounds.Bottom - inset),
                    new PointF(bounds.Left, bounds.Top + inset)
                });
                octagon.CloseFigure();
                return octagon;
            }

            float radius = string.Equals(shapeKind, "Rectangle", StringComparison.OrdinalIgnoreCase) ? Math.Min(18f, bounds.Height * 0.10f) : Math.Min(14f, bounds.Height * 0.07f);
            return CreateRoundedRectangle(bounds, radius);
        }

        private static Rectangle FitRectangle(int sourceWidth, int sourceHeight, Rectangle target, bool allowUpscale)
        {
            if (sourceWidth <= 0 || sourceHeight <= 0)
                return target;

            double scale = Math.Min((double)target.Width / sourceWidth, (double)target.Height / sourceHeight);
            if (!allowUpscale)
                scale = Math.Min(1.0, scale);
            int width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
            int height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
            int x = target.X + (target.Width - width) / 2;
            int y = target.Y + (target.Height - height) / 2;
            return new Rectangle(x, y, width, height);
        }

        private static Rectangle FillRectangle(int sourceWidth, int sourceHeight, Rectangle target)
        {
            if (sourceWidth <= 0 || sourceHeight <= 0)
                return target;

            double scale = Math.Max((double)target.Width / sourceWidth, (double)target.Height / sourceHeight);
            int width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
            int height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
            int x = target.X + (target.Width - width) / 2;
            int y = target.Y + (target.Height - height) / 2;
            return new Rectangle(x, y, width, height);
        }

        private static Bitmap ExtractVisibleRegion(Bitmap source, TemplateDefinition template)
        {
            if (template == null || !template.ImageProcessingEnabled || template.VisibleWidth <= 0 || template.VisibleHeight <= 0)
                return new Bitmap(source);

            Rectangle bounds = new Rectangle(0, 0, source.Width, source.Height);
            Rectangle visible = Rectangle.Intersect(bounds, new Rectangle(template.VisibleX, template.VisibleY, template.VisibleWidth, template.VisibleHeight));
            if (visible.Width <= 0 || visible.Height <= 0)
                return new Bitmap(source);
            return source.Clone(visible, PixelFormat.Format32bppArgb);
        }

        private static Bitmap ResizeForPreview(Bitmap rendered, int maxWidth, int maxHeight, bool drawGuide, TemplateDefinition template, Color guideColor)
        {
            double scale = Math.Min((double)maxWidth / rendered.Width, (double)maxHeight / rendered.Height);
            scale = Math.Min(1.0, scale);
            int width = Math.Max(1, (int)Math.Round(rendered.Width * scale));
            int height = Math.Max(1, (int)Math.Round(rendered.Height * scale));
            Bitmap preview = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(preview))
            {
                ConfigureGraphics(g);
                g.Clear(Color.FromArgb(36, 36, 36));
                g.DrawImage(rendered, new Rectangle(0, 0, width, height));
                if (drawGuide)
                    DrawGuideOverlay(g, template, scale, guideColor);
            }
            return preview;
        }

        private static Bitmap ResizeSimple(Bitmap rendered, int maxWidth, int maxHeight, Color background)
        {
            double scale = Math.Min((double)maxWidth / rendered.Width, (double)maxHeight / rendered.Height);
            scale = Math.Min(1.0, scale);
            int width = Math.Max(1, (int)Math.Round(rendered.Width * scale));
            int height = Math.Max(1, (int)Math.Round(rendered.Height * scale));
            Bitmap preview = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(preview))
            {
                ConfigureGraphics(g);
                g.Clear(background);
                g.DrawImage(rendered, new Rectangle(0, 0, width, height));
            }
            return preview;
        }

        private static void DrawGuideOverlay(Graphics g, TemplateDefinition template, double scale, Color guideColor)
        {
            RectangleF visible = new RectangleF(
                (float)(template.VisibleX * scale),
                (float)(template.VisibleY * scale),
                (float)(template.VisibleWidth * scale),
                (float)(template.VisibleHeight * scale));

            float previewWidth = (float)(template.TextureWidth * scale);
            float previewHeight = (float)(template.TextureHeight * scale);

            // Dim only the portions of the generated texture that are outside the mapped area.
            // This is used by Poster; Mural maps the entire texture so no dimming is visible.
            using (SolidBrush dim = new SolidBrush(Color.FromArgb(115, 0, 0, 0)))
            {
                if (visible.Y > 0)
                    g.FillRectangle(dim, 0, 0, previewWidth, visible.Y);
                if (visible.Bottom < previewHeight)
                    g.FillRectangle(dim, 0, visible.Bottom, previewWidth, previewHeight - visible.Bottom);
                if (visible.X > 0)
                    g.FillRectangle(dim, 0, visible.Y, visible.X, visible.Height);
                if (visible.Right < previewWidth)
                    g.FillRectangle(dim, visible.Right, visible.Y, previewWidth - visible.Right, visible.Height);
            }

            float penWidth = Math.Max(2.0f, Math.Min(4.0f, (float)scale * 3.0f));
            using (Pen outer = new Pen(Color.FromArgb(185, 0, 0, 0), penWidth + 2.0f))
            using (Pen guide = new Pen(guideColor, penWidth))
            {
                outer.Alignment = PenAlignment.Inset;
                guide.Alignment = PenAlignment.Inset;
                RectangleF outlineRect = new RectangleF(visible.X, visible.Y, Math.Max(1, visible.Width - 1), Math.Max(1, visible.Height - 1));
                g.DrawRectangle(outer, outlineRect.X, outlineRect.Y, outlineRect.Width, outlineRect.Height);
                g.DrawRectangle(guide, outlineRect.X, outlineRect.Y, outlineRect.Width, outlineRect.Height);
            }
        }

        private static void EnsureFamilyReferenceInternal(TemplateDefinition template, ImagePlacementState placement)
        {
            if (template == null || placement == null || (placement.ReferenceWidth > 0 && placement.ReferenceHeight > 0))
                return;

            if (IsPosterTemplate(template))
            {
                placement.ReferenceWidth = PosterFamilyReferenceWidth;
                placement.ReferenceHeight = PosterFamilyReferenceHeight;
            }
            else if (IsHangingSignTemplate(template))
            {
                placement.ReferenceWidth = HangingSignFamilyReferenceWidth;
                placement.ReferenceHeight = HangingSignFamilyReferenceHeight;
            }
            else if (IsWallSignTemplate(template))
            {
                placement.ReferenceWidth = WallSignFamilyReferenceWidth;
                placement.ReferenceHeight = WallSignFamilyReferenceHeight;
            }
            else if (IsRugTemplate(template))
            {
                placement.ReferenceWidth = RugFamilyReferenceWidth;
                placement.ReferenceHeight = RugFamilyReferenceHeight;
            }
        }

        private static Rectangle CentreRectangle(Rectangle bounds, int width, int height)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);
            return new Rectangle(
                bounds.X + (bounds.Width - width) / 2,
                bounds.Y + (bounds.Height - height) / 2,
                width, height);
        }

        private static Rectangle CentreReferenceRectangle(Rectangle currentGuide, ImagePlacementState placement)
        {
            int width = placement != null && placement.ReferenceWidth > 0 ? placement.ReferenceWidth : currentGuide.Width;
            int height = placement != null && placement.ReferenceHeight > 0 ? placement.ReferenceHeight : currentGuide.Height;
            int centreX = currentGuide.X + currentGuide.Width / 2;
            int centreY = currentGuide.Y + currentGuide.Height / 2;
            return new Rectangle(centreX - width / 2, centreY - height / 2, width, height);
        }

        private static void DrawReferencedImageClipped(Graphics g, Image source, Rectangle currentGuide, string fitMode, ImagePlacementState placement)
        {
            Rectangle reference = CentreReferenceRectangle(currentGuide, placement);
            GraphicsState state = g.Save();
            g.SetClip(currentGuide);
            DrawImageWithoutClip(g, source, reference, fitMode, placement);
            g.Restore(state);
        }

        private static void DrawReferencedImageWithoutClip(Graphics g, Image source, Rectangle currentGuide, string fitMode, ImagePlacementState placement)
        {
            Rectangle reference = CentreReferenceRectangle(currentGuide, placement);
            DrawImageWithoutClip(g, source, reference, fitMode, placement);
        }

        private static void DrawDimmedGuide(Graphics g, Rectangle canvasBounds, Rectangle guide, Color guideColor)
        {
            using (SolidBrush dim = new SolidBrush(Color.FromArgb(115, 0, 0, 0)))
            {
                if (guide.Top > canvasBounds.Top)
                    g.FillRectangle(dim, canvasBounds.Left, canvasBounds.Top, canvasBounds.Width, guide.Top - canvasBounds.Top);
                if (guide.Bottom < canvasBounds.Bottom)
                    g.FillRectangle(dim, canvasBounds.Left, guide.Bottom, canvasBounds.Width, canvasBounds.Bottom - guide.Bottom);
                if (guide.Left > canvasBounds.Left)
                    g.FillRectangle(dim, canvasBounds.Left, guide.Top, guide.Left - canvasBounds.Left, guide.Height);
                if (guide.Right < canvasBounds.Right)
                    g.FillRectangle(dim, guide.Right, guide.Top, canvasBounds.Right - guide.Right, guide.Height);
            }

            using (Pen outer = new Pen(Color.FromArgb(185, 0, 0, 0), 4f))
            using (Pen pen = new Pen(guideColor, 2f))
            {
                outer.Alignment = PenAlignment.Inset;
                pen.Alignment = PenAlignment.Inset;
                Rectangle outline = new Rectangle(guide.X, guide.Y, Math.Max(1, guide.Width - 1), Math.Max(1, guide.Height - 1));
                g.DrawRectangle(outer, outline);
                g.DrawRectangle(pen, outline);
            }
        }

        private static void DrawImage(Graphics g, Image source, Rectangle target, string fitMode, ImagePlacementState placement)
        {
            float zoom = placement == null || placement.Zoom <= 0 ? 1.0f : placement.Zoom;
            float offsetX = placement == null ? 0.0f : placement.OffsetX;
            float offsetY = placement == null ? 0.0f : placement.OffsetY;
            string mode = (fitMode ?? "Fill").Trim();

            int width;
            int height;
            if (string.Equals(mode, "Stretch", StringComparison.OrdinalIgnoreCase))
            {
                width = Math.Max(1, (int)Math.Round(target.Width * zoom));
                height = Math.Max(1, (int)Math.Round(target.Height * zoom));
            }
            else
            {
                double sx = (double)target.Width / source.Width;
                double sy = (double)target.Height / source.Height;
                bool fit = string.Equals(mode, "Fit", StringComparison.OrdinalIgnoreCase);
                double baseScale = fit ? Math.Min(sx, sy) : Math.Max(sx, sy);
                width = Math.Max(1, (int)Math.Round(source.Width * baseScale * zoom));
                height = Math.Max(1, (int)Math.Round(source.Height * baseScale * zoom));
            }

            int x = target.X + (target.Width - width) / 2 + (int)Math.Round(offsetX);
            int y = target.Y + (target.Height - height) / 2 + (int)Math.Round(offsetY);
            float rotationDegrees = placement == null ? 0.0f : placement.RotationDegrees;

            GraphicsState state = g.Save();
            g.SetClip(target);
            if (Math.Abs(rotationDegrees) > 0.0001f)
            {
                // Rotate around the centre of the positioned artwork, not the viewport.
                // This preserves the user's pan and zoom while changing only orientation.
                float centreX = x + width / 2.0f;
                float centreY = y + height / 2.0f;
                g.TranslateTransform(centreX, centreY);
                g.RotateTransform(rotationDegrees);
                g.TranslateTransform(-centreX, -centreY);
            }
            g.DrawImage(source, new Rectangle(x, y, width, height));
            g.Restore(state);
        }

        private static GraphicsPath CreateRoundedRectangle(RectangleF bounds, float radius)
        {
            float diameter = radius * 2f;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Bitmap LoadUnlockedBitmap(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            using (MemoryStream stream = new MemoryStream(bytes))
            using (Image image = Image.FromStream(stream))
                return new Bitmap(image);
        }

        public string CreateWorkshopFamilyCompositePreview(List<string> previewPaths, string layoutStyle)
        {
            List<string> valid = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (previewPaths != null)
            {
                for (int i = 0; i < previewPaths.Count; i++)
                {
                    string path = previewPaths[i];
                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                        continue;
                    string fullPath = Path.GetFullPath(path);
                    if (seen.Add(fullPath))
                        valid.Add(fullPath);
                }
            }

            if (valid.Count == 0)
                return "";

            string root = Path.Combine(AppInfo.LocalDataRoot, "Prepared");
            Directory.CreateDirectory(root);
            string outputPath = Path.Combine(root, "WorkshopFamilyPreview_" + Guid.NewGuid().ToString("N") + ".png");
            using (Bitmap preview = RenderWorkshopFamilyComposite(valid, layoutStyle, 1024, 1024))
                preview.Save(outputPath, ImageFormat.Png);
            return outputPath;
        }

        private static Bitmap RenderWorkshopFamilyComposite(List<string> previewPaths, string layoutStyle, int canvasWidth, int canvasHeight)
        {
            List<Bitmap> images = new List<Bitmap>();
            try
            {
                for (int i = 0; i < previewPaths.Count; i++)
                {
                    string path = previewPaths[i];
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                        images.Add(LoadUnlockedBitmap(path));
                }

                if (images.Count == 0)
                    throw new InvalidOperationException("No valid preview images were available for the family Workshop preview.");

                Bitmap canvas = new Bitmap(canvasWidth, canvasHeight, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(canvas))
                {
                    ConfigureGraphics(g);
                    g.Clear(Color.Transparent);

                    if (images.Count == 1)
                    {
                        DrawWorkshopFamilyTile(g, images[0], new Rectangle(160, 140, canvasWidth - 320, canvasHeight - 280), 0f, true);
                        return canvas;
                    }

                    string style = NormaliseWorkshopFamilyPreviewStyle(layoutStyle);
                    if (string.Equals(style, "Hero Grid", StringComparison.OrdinalIgnoreCase))
                        DrawWorkshopHeroGrid(g, images, canvasWidth, canvasHeight);
                    else if (string.Equals(style, "Staggered Mosaic", StringComparison.OrdinalIgnoreCase))
                        DrawWorkshopStaggeredMosaic(g, images, canvasWidth, canvasHeight);
                    else if (string.Equals(style, "Central Hero", StringComparison.OrdinalIgnoreCase))
                        DrawWorkshopCentralHero(g, images, canvasWidth, canvasHeight);
                    else if (string.Equals(style, "Framed Focus", StringComparison.OrdinalIgnoreCase))
                        DrawWorkshopFramedFocus(g, images, canvasWidth, canvasHeight);
                    else if (string.Equals(style, "Left Hero Stack", StringComparison.OrdinalIgnoreCase))
                        DrawWorkshopLeftHeroStack(g, images, canvasWidth, canvasHeight);
                    else if (string.Equals(style, "Quadrant Mix", StringComparison.OrdinalIgnoreCase))
                        DrawWorkshopQuadrantMix(g, images, canvasWidth, canvasHeight);
                    else if (string.Equals(style, "Double Feature", StringComparison.OrdinalIgnoreCase))
                        DrawWorkshopDoubleFeature(g, images, canvasWidth, canvasHeight);
                    else
                        DrawWorkshopSimpleGrid(g, images, canvasWidth, canvasHeight);
                }
                return canvas;
            }
            finally
            {
                for (int i = 0; i < images.Count; i++)
                    images[i].Dispose();
            }
        }

        private static string NormaliseWorkshopFamilyPreviewStyle(string layoutStyle)
        {
            string value = (layoutStyle ?? "").Trim();
            if (string.Equals(value, "Hero Grid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Hero + Grid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Hero+Grid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "HeroGrid", StringComparison.OrdinalIgnoreCase))
                return "Hero Grid";
            if (string.Equals(value, "Staggered Mosaic", StringComparison.OrdinalIgnoreCase))
                return "Staggered Mosaic";
            if (string.Equals(value, "Central Hero", StringComparison.OrdinalIgnoreCase))
                return "Central Hero";
            if (string.Equals(value, "Framed Focus", StringComparison.OrdinalIgnoreCase))
                return "Framed Focus";
            if (string.Equals(value, "Left Hero Stack", StringComparison.OrdinalIgnoreCase))
                return "Left Hero Stack";
            if (string.Equals(value, "Quadrant Mix", StringComparison.OrdinalIgnoreCase))
                return "Quadrant Mix";
            if (string.Equals(value, "Double Feature", StringComparison.OrdinalIgnoreCase))
                return "Double Feature";
            return "Simple Grid";
        }

        private static void DrawWorkshopSimpleGrid(Graphics g, List<Bitmap> images, int canvasWidth, int canvasHeight)
        {
            int count = images.Count;
            int cols = (int)Math.Ceiling(Math.Sqrt(count));
            cols = Math.Max(1, Math.Min(4, cols));
            int rows = (int)Math.Ceiling((double)count / cols);
            int gap = count > 9 ? 16 : 28;
            int margin = count > 9 ? 45 : 72;
            int cellWidth = (canvasWidth - margin * 2 - gap * Math.Max(0, cols - 1)) / cols;
            int cellHeight = (canvasHeight - margin * 2 - gap * Math.Max(0, rows - 1)) / rows;
            int tileSize = Math.Max(90, Math.Min(cellWidth, cellHeight));
            int totalHeight = rows * tileSize + Math.Max(0, rows - 1) * gap;
            int startY = (canvasHeight - totalHeight) / 2;

            for (int i = 0; i < count; i++)
            {
                int row = i / cols;
                int col = i % cols;
                int usedCols = row == rows - 1 ? Math.Min(cols, count - row * cols) : cols;
                int rowWidth = usedCols * tileSize + Math.Max(0, usedCols - 1) * gap;
                int startX = (canvasWidth - rowWidth) / 2;
                Rectangle tile = new Rectangle(startX + col * (tileSize + gap), startY + row * (tileSize + gap), tileSize, tileSize);
                DrawWorkshopFamilyTile(g, images[i], tile, 0f, i == 0);
            }
        }

        private static void DrawWorkshopHeroGrid(Graphics g, List<Bitmap> images, int canvasWidth, int canvasHeight)
        {
            Rectangle hero = new Rectangle(58, 92, 500, 840);
            DrawWorkshopFamilyTile(g, images[0], hero, 0f, true);

            int rest = images.Count - 1;
            if (rest <= 0)
                return;
            int cols = rest <= 3 ? 1 : 2;
            int rows = (int)Math.Ceiling((double)rest / cols);
            int gap = 18;
            int areaX = 590;
            int areaWidth = canvasWidth - areaX - 54;
            int areaHeight = canvasHeight - 120;
            int cellWidth = (areaWidth - gap * Math.Max(0, cols - 1)) / cols;
            int cellHeight = (areaHeight - gap * Math.Max(0, rows - 1)) / rows;
            int tileSize = Math.Max(90, Math.Min(cellWidth, cellHeight));
            int totalHeight = rows * tileSize + Math.Max(0, rows - 1) * gap;
            int startY = (canvasHeight - totalHeight) / 2;

            for (int i = 1; i < images.Count; i++)
            {
                int index = i - 1;
                int row = index / cols;
                int col = index % cols;
                int usedCols = row == rows - 1 ? Math.Min(cols, rest - row * cols) : cols;
                int rowWidth = usedCols * tileSize + Math.Max(0, usedCols - 1) * gap;
                int startX = areaX + Math.Max(0, (areaWidth - rowWidth) / 2);
                Rectangle tile = new Rectangle(startX + col * (tileSize + gap), startY + row * (tileSize + gap), tileSize, tileSize);
                DrawWorkshopFamilyTile(g, images[i], tile, 0f, false);
            }
        }

        private static void DrawWorkshopStaggeredMosaic(Graphics g, List<Bitmap> images, int canvasWidth, int canvasHeight)
        {
            if (images.Count > 9)
            {
                DrawWorkshopSimpleGrid(g, images, canvasWidth, canvasHeight);
                return;
            }

            Rectangle[] slots = new Rectangle[]
            {
                new Rectangle(54, 70, 335, 335),
                new Rectangle(425, 55, 245, 245),
                new Rectangle(706, 96, 210, 210),
                new Rectangle(96, 445, 210, 210),
                new Rectangle(343, 375, 315, 315),
                new Rectangle(700, 350, 260, 260),
                new Rectangle(78, 700, 245, 245),
                new Rectangle(382, 730, 205, 205),
                new Rectangle(650, 665, 310, 310)
            };

            for (int i = 0; i < images.Count; i++)
                DrawWorkshopFamilyTile(g, images[i], slots[i], 0f, i == 0);
        }

        private static void DrawWorkshopCentralHero(Graphics g, List<Bitmap> images, int canvasWidth, int canvasHeight)
        {
            int heroSize = 390;
            Rectangle hero = new Rectangle((canvasWidth - heroSize) / 2, (canvasHeight - heroSize) / 2, heroSize, heroSize);
            DrawWorkshopFamilyTile(g, images[0], hero, 0f, true);

            int rest = images.Count - 1;
            if (rest <= 0)
                return;
            for (int i = 0; i < rest; i++)
            {
                int ring = i / 8;
                int ringIndex = i % 8;
                int ringCount = Math.Min(8, rest - ring * 8);
                double angle = (-Math.PI / 2.0) + (2.0 * Math.PI * ringIndex / Math.Max(1, ringCount));
                int tileSize = ring == 0 ? 190 : 125;
                double radius = ring == 0 ? 365.0 : 455.0;
                int centreX = canvasWidth / 2 + (int)Math.Round(Math.Cos(angle) * radius);
                int centreY = canvasHeight / 2 + (int)Math.Round(Math.Sin(angle) * radius);
                Rectangle tile = new Rectangle(centreX - tileSize / 2, centreY - tileSize / 2, tileSize, tileSize);
                DrawWorkshopFamilyTile(g, images[i + 1], tile, 0f, false);
            }
        }

        private static void DrawWorkshopFramedFocus(Graphics g, List<Bitmap> images, int canvasWidth, int canvasHeight)
        {
            if (images.Count > 13)
            {
                DrawWorkshopCentralHero(g, images, canvasWidth, canvasHeight);
                return;
            }

            int heroSize = 400;
            Rectangle hero = new Rectangle((canvasWidth - heroSize) / 2, (canvasHeight - heroSize) / 2, heroSize, heroSize);
            DrawWorkshopFamilyTile(g, images[0], hero, 0f, true);

            Rectangle[] slots = new Rectangle[]
            {
                new Rectangle(170, 70, 170, 170), new Rectangle(427, 56, 170, 170), new Rectangle(684, 70, 170, 170),
                new Rectangle(76, 274, 170, 170), new Rectangle(778, 274, 170, 170),
                new Rectangle(76, 580, 170, 170), new Rectangle(778, 580, 170, 170),
                new Rectangle(170, 784, 170, 170), new Rectangle(427, 798, 170, 170), new Rectangle(684, 784, 170, 170),
                new Rectangle(76, 427, 150, 150), new Rectangle(798, 427, 150, 150)
            };
            for (int i = 1; i < images.Count; i++)
                DrawWorkshopFamilyTile(g, images[i], slots[i - 1], 0f, false);
        }

        private static void DrawWorkshopLeftHeroStack(Graphics g, List<Bitmap> images, int canvasWidth, int canvasHeight)
        {
            Rectangle hero = new Rectangle(58, 78, 470, 868);
            DrawWorkshopFamilyTile(g, images[0], hero, 0f, true);

            int rest = images.Count - 1;
            if (rest <= 0)
                return;
            int cols = rest <= 4 ? 1 : 2;
            int rows = (int)Math.Ceiling((double)rest / cols);
            int gap = 18;
            int areaX = 565;
            int areaWidth = canvasWidth - areaX - 52;
            int areaHeight = canvasHeight - 110;
            int cellWidth = (areaWidth - gap * Math.Max(0, cols - 1)) / cols;
            int cellHeight = (areaHeight - gap * Math.Max(0, rows - 1)) / rows;
            int tileSize = Math.Max(90, Math.Min(cellWidth, cellHeight));
            int totalHeight = rows * tileSize + Math.Max(0, rows - 1) * gap;
            int startY = (canvasHeight - totalHeight) / 2;

            for (int i = 1; i < images.Count; i++)
            {
                int index = i - 1;
                int row = index / cols;
                int col = index % cols;
                Rectangle tile = new Rectangle(areaX + col * (tileSize + gap), startY + row * (tileSize + gap), tileSize, tileSize);
                DrawWorkshopFamilyTile(g, images[i], tile, 0f, false);
            }
        }

        private static void DrawWorkshopQuadrantMix(Graphics g, List<Bitmap> images, int canvasWidth, int canvasHeight)
        {
            if (images.Count > 8)
            {
                DrawWorkshopSimpleGrid(g, images, canvasWidth, canvasHeight);
                return;
            }

            Rectangle[] slots = new Rectangle[]
            {
                new Rectangle(75, 80, 350, 350),
                new Rectangle(585, 72, 300, 300),
                new Rectangle(90, 610, 290, 290),
                new Rectangle(590, 570, 350, 350),
                new Rectangle(415, 125, 150, 150),
                new Rectangle(420, 725, 150, 150),
                new Rectangle(120, 430, 150, 150),
                new Rectangle(755, 400, 150, 150)
            };
            for (int i = 0; i < images.Count; i++)
                DrawWorkshopFamilyTile(g, images[i], slots[i], 0f, i == 0);
        }

        private static void DrawWorkshopDoubleFeature(Graphics g, List<Bitmap> images, int canvasWidth, int canvasHeight)
        {
            if (images.Count == 2)
            {
                DrawWorkshopFamilyTile(g, images[0], new Rectangle(110, 180, 360, 660), 0f, true);
                DrawWorkshopFamilyTile(g, images[1], new Rectangle(554, 180, 360, 660), 0f, true);
                return;
            }

            DrawWorkshopFamilyTile(g, images[0], new Rectangle(105, 220, 345, 575), 0f, true);
            DrawWorkshopFamilyTile(g, images[1], new Rectangle(574, 220, 345, 575), 0f, true);

            int rest = images.Count - 2;
            int topCount = (rest + 1) / 2;
            int bottomCount = rest - topCount;
            DrawWorkshopSupportingRow(g, images, 2, topCount, 70, canvasWidth);
            if (bottomCount > 0)
                DrawWorkshopSupportingRow(g, images, 2 + topCount, bottomCount, 815, canvasWidth);
        }

        private static void DrawWorkshopSupportingRow(Graphics g, List<Bitmap> images, int startIndex, int count, int y, int canvasWidth)
        {
            if (count <= 0)
                return;
            int gap = 16;
            int maxWidth = canvasWidth - 100;
            int tileSize = Math.Min(150, (maxWidth - gap * Math.Max(0, count - 1)) / count);
            tileSize = Math.Max(80, tileSize);
            int totalWidth = count * tileSize + Math.Max(0, count - 1) * gap;
            int startX = (canvasWidth - totalWidth) / 2;
            for (int i = 0; i < count; i++)
            {
                int imageIndex = startIndex + i;
                if (imageIndex >= images.Count)
                    break;
                Rectangle tile = new Rectangle(startX + i * (tileSize + gap), y, tileSize, tileSize);
                DrawWorkshopFamilyTile(g, images[imageIndex], tile, 0f, false);
            }
        }

        private static void DrawWorkshopFamilyTile(Graphics g, Image source, Rectangle bounds, float rotationDegrees, bool hero)
        {
            if (g == null || source == null)
                return;

            int inset = hero ? 18 : 12;
            GraphicsState state = g.Save();
            try
            {
                if (Math.Abs(rotationDegrees) > 0.001f)
                {
                    float centreX = bounds.X + bounds.Width / 2.0f;
                    float centreY = bounds.Y + bounds.Height / 2.0f;
                    g.TranslateTransform(centreX, centreY);
                    g.RotateTransform(rotationDegrees);
                    g.TranslateTransform(-centreX, -centreY);
                }

                // Draw each family preview directly onto the transparent canvas; no background card is added.
                Rectangle imageBounds = Rectangle.Inflate(bounds, -inset, -inset);
                DrawImageWithoutClip(g, source, imageBounds, "Fit", new ImagePlacementState { Zoom = hero ? 0.98f : 0.95f });
            }
            finally
            {
                g.Restore(state);
            }
        }

        private static void ConfigureInteractiveGraphics(Graphics g)
        {
            // The interactive editor works at the on-screen preview resolution only.
            // HighQualityBilinear keeps artwork smooth while avoiding the cost of the
            // full build-quality bicubic pipeline on every pointer movement.
            g.CompositingQuality = CompositingQuality.HighSpeed;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            g.SmoothingMode = SmoothingMode.AntiAlias;
        }

        private static void ConfigureGraphics(Graphics g)
        {
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;
        }
    }
}
