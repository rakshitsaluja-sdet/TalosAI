// MCPBridge/Tools/ImageToolHandler.cs
using ImageMagick;
using ImageMagick.Drawing;
using McpBridge.Models;

namespace McpBridge.Tools;

public class ImageToolHandler
{
    // ?? Compare Images ????????????????????????????????????????????????
    public ToolResponse CompareImages(Dictionary<string, object> args)
    {
        var baselineImage = args["baseline_image"].ToString()!;
        var currentImage = args["current_image"].ToString()!;
        var diffImage = args.GetValueOrDefault("diff_image", "diff.png")?.ToString() ?? "diff.png";
        var threshold = args.ContainsKey("threshold") 
            ? double.Parse(args["threshold"].ToString()!) 
            : 0.01;

        try
        {
            if (!File.Exists(baselineImage))
                return ToolResponse.Fail($"Baseline image not found: {baselineImage}");

            if (!File.Exists(currentImage))
                return ToolResponse.Fail($"Current image not found: {currentImage}");

            using var baseline = new MagickImage(baselineImage);
            using var current = new MagickImage(currentImage);

            var distortion = baseline.Compare(current, ErrorMetric.Absolute);
            
            // Create diff image by comparing
            using var diff = baseline.Clone();
            diff.Composite(current, CompositeOperator.Difference);
            diff.Write(diffImage);

            var areSimilar = distortion < threshold;

            return ToolResponse.Ok(new
            {
                status = "ok",
                areSimilar,
                difference = distortion,
                threshold,
                diffImagePath = diffImage,
                baselineSize = new { width = baseline.Width, height = baseline.Height },
                currentSize = new { width = current.Width, height = current.Height }
            });
        }
        catch (Exception ex)
        {
            return ToolResponse.Fail($"Image comparison failed: {ex.Message}");
        }
    }

    // ?? Resize Image ??????????????????????????????????????????????????
    public ToolResponse ResizeImage(Dictionary<string, object> args)
    {
        var inputPath = args["input"].ToString()!;
        var outputPath = args["output"].ToString()!;
        var width = (uint)int.Parse(args["width"].ToString()!);
        var height = (uint)int.Parse(args["height"].ToString()!);

        try
        {
            using var image = new MagickImage(inputPath);
            image.Resize(width, height);
            image.Write(outputPath);

            return ToolResponse.Ok(new
            {
                status = "ok",
                inputPath,
                outputPath,
                newSize = new { width, height }
            });
        }
        catch (Exception ex)
        {
            return ToolResponse.Fail($"Image resize failed: {ex.Message}");
        }
    }

    // ?? Crop Image ????????????????????????????????????????????????????
    public ToolResponse CropImage(Dictionary<string, object> args)
    {
        var inputPath = args["input"].ToString()!;
        var outputPath = args["output"].ToString()!;
        var x = int.Parse(args["x"].ToString()!);
        var y = int.Parse(args["y"].ToString()!);
        var width = (uint)int.Parse(args["width"].ToString()!);
        var height = (uint)int.Parse(args["height"].ToString()!);

        try
        {
            using var image = new MagickImage(inputPath);
            image.Crop(new MagickGeometry(x, y, width, height));
            image.Write(outputPath);

            return ToolResponse.Ok(new
            {
                status = "ok",
                inputPath,
                outputPath,
                cropArea = new { x, y, width, height }
            });
        }
        catch (Exception ex)
        {
            return ToolResponse.Fail($"Image crop failed: {ex.Message}");
        }
    }

    // ?? Annotate Image ????????????????????????????????????????????????
    public ToolResponse AnnotateImage(Dictionary<string, object> args)
    {
        var inputPath = args["input"].ToString()!;
        var outputPath = args["output"].ToString()!;
        var text = args["text"].ToString()!;
        var x = args.ContainsKey("x") ? int.Parse(args["x"].ToString()!) : 10;
        var y = args.ContainsKey("y") ? int.Parse(args["y"].ToString()!) : 10;

        try
        {
            using var image = new MagickImage(inputPath);

            // Draw directly at the requested (x, y) with the intended font size/color —
            // IMagickImage.Annotate(text, gravity) ignores both position and a separately
            // constructed MagickReadSettings, so drawing must go through Drawables instead.
            new Drawables()
                .FontPointSize(20)
                .FillColor(MagickColors.Red)
                .TextAlignment(TextAlignment.Left)
                .Text(x, y, text)
                .Draw(image);

            image.Write(outputPath);

            return ToolResponse.Ok(new
            {
                status = "ok",
                inputPath,
                outputPath,
                annotation = text
            });
        }
        catch (Exception ex)
        {
            return ToolResponse.Fail($"Image annotation failed: {ex.Message}");
        }
    }

    // ?? Convert Image Format ??????????????????????????????????????????
    public ToolResponse ConvertImageFormat(Dictionary<string, object> args)
    {
        var inputPath = args["input"].ToString()!;
        var outputPath = args["output"].ToString()!;
        var format = args["format"].ToString()!.ToLower();

        try
        {
            using var image = new MagickImage(inputPath);
            
            image.Format = format.ToUpper() switch
            {
                "PNG" => MagickFormat.Png,
                "JPG" or "JPEG" => MagickFormat.Jpeg,
                "GIF" => MagickFormat.Gif,
                "BMP" => MagickFormat.Bmp,
                "WEBP" => MagickFormat.WebP,
                _ => MagickFormat.Png
            };

            image.Write(outputPath);

            return ToolResponse.Ok(new
            {
                status = "ok",
                inputPath,
                outputPath,
                format = format.ToUpper()
            });
        }
        catch (Exception ex)
        {
            return ToolResponse.Fail($"Format conversion failed: {ex.Message}");
        }
    }

    // ?? Get Image Properties ??????????????????????????????????????????
    public ToolResponse GetImageProperties(Dictionary<string, object> args)
    {
        var imagePath = args["image"].ToString()!;

        try
        {
            using var image = new MagickImage(imagePath);

            return ToolResponse.Ok(new
            {
                status = "ok",
                imagePath,
                width = image.Width,
                height = image.Height,
                format = image.Format.ToString(),
                fileSize = new FileInfo(imagePath).Length,
                colorSpace = image.ColorSpace.ToString(),
                depth = image.Depth,
                quality = image.Quality
            });
        }
        catch (Exception ex)
        {
            return ToolResponse.Fail($"Failed to get image properties: {ex.Message}");
        }
    }

    // ?? Create Screenshot Comparison ??????????????????????????????????
    public ToolResponse CreateScreenshotComparison(Dictionary<string, object> args)
    {
        var baseline = args["baseline"].ToString()!;
        var current = args["current"].ToString()!;
        var output = args.GetValueOrDefault("output", "comparison.png")?.ToString() ?? "comparison.png";

        try
        {
            using var baselineImg = new MagickImage(baseline);
            using var currentImg = new MagickImage(current);

            var distortion = baselineImg.Compare(currentImg, ErrorMetric.Absolute);
            
            // Create diff image
            using var diffImg = baselineImg.Clone();
            diffImg.Composite(currentImg, CompositeOperator.Difference);
            
            // Create side-by-side comparison
            using var combined = new MagickImageCollection();
            
            combined.Add(baselineImg.Clone());
            combined.Add(currentImg.Clone());
            combined.Add(diffImg.Clone());

            using var result = combined.AppendHorizontally();
            result.Write(output);

            return ToolResponse.Ok(new
            {
                status = "ok",
                baselineImage = baseline,
                currentImage = current,
                outputImage = output,
                difference = distortion,
                verdict = distortion < 0.01 ? "PASS" : "FAIL"
            });
        }
        catch (Exception ex)
        {
            return ToolResponse.Fail($"Screenshot comparison failed: {ex.Message}");
        }
    }

    // ?? Batch Compare Screenshots ?????????????????????????????????????
    public ToolResponse BatchCompareScreenshots(Dictionary<string, object> args)
    {
        var baselineDir = args["baseline_dir"].ToString()!;
        var currentDir = args["current_dir"].ToString()!;
        var outputDir = args.GetValueOrDefault("output_dir", "diffs")?.ToString() ?? "diffs";
        var threshold = args.ContainsKey("threshold") 
            ? double.Parse(args["threshold"].ToString()!) 
            : 0.01;

        try
        {
            Directory.CreateDirectory(outputDir);

            var baselineFiles = Directory.GetFiles(baselineDir, "*.png");
            var results = new List<object>();

            foreach (var baselinePath in baselineFiles)
            {
                var fileName = Path.GetFileName(baselinePath);
                var currentPath = Path.Combine(currentDir, fileName);
                
                if (!File.Exists(currentPath))
                {
                    results.Add(new { file = fileName, status = "missing", difference = -1.0 });
                    continue;
                }

                var diffPath = Path.Combine(outputDir, $"diff_{fileName}");

                using var baseline = new MagickImage(baselinePath);
                using var current = new MagickImage(currentPath);

                var distortion = baseline.Compare(current, ErrorMetric.Absolute);
                
                // Create diff
                using var diff = baseline.Clone();
                diff.Composite(current, CompositeOperator.Difference);
                diff.Write(diffPath);

                results.Add(new
                {
                    file = fileName,
                    status = distortion < threshold ? "pass" : "fail",
                    difference = distortion,
                    diffImage = diffPath
                });
            }

            var passCount = results.Count(r => ((dynamic)r).status == "pass");
            var failCount = results.Count(r => ((dynamic)r).status == "fail");
            var missingCount = results.Count(r => ((dynamic)r).status == "missing");

            return ToolResponse.Ok(new
            {
                status = "ok",
                totalFiles = results.Count,
                passed = passCount,
                failed = failCount,
                missing = missingCount,
                results
            });
        }
        catch (Exception ex)
        {
            return ToolResponse.Fail($"Batch comparison failed: {ex.Message}");
        }
    }
}
