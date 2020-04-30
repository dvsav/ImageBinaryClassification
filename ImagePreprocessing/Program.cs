using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ImagePreprocessing
{
    // Usage example:
    // In cmd.exe write:
    // ImagePreprocessing "C:\MyImages" -print-files -rename -bmp-to-jpg -lowres -gray
    // [-print-files] - prints all files found in "C:\MyImages".
    // [-rename]      - renames all files to random file names.
    // [-bmp-to-jpg]  - converts all BMP files to JPEG (doesn't delete BMP files).
    // [-lowres]      - creates a copy of "C:\MyImages" folder structure in "C:\MyImages (Low Resolution)"
    //                  and creates there a low-resolution (320 x 240) copy of each image from "C:\MyImages".
    // [-gray]        - creates a copy of the folder structure of "C:\MyImages (Low Resolution)"
    //                  (or "C:\MyImages" - whichever exists) in "C:\MyImages (Grayscale)"
    //                  and creates there a grayscale 8-bits-per-pixel copy of each image from "C:\MyImages".
    class Program
    {
        static void Main(string[] args)
        {
            // arg[0] - path to the folder containing images
            if (args.Length < 1)
                throw new Exception($"ERROR: Too few command-line arguments ({args.Length})!"); ;

            var directory_path = args[0];
            Console.WriteLine($"Directory = {args[0]}");

            bool print_files = args.Contains("-print-files");
            bool rename_files = args.Contains("-rename");
            bool bmp_to_jpg = args.Contains("-bmp-to-jpg");
            bool lowres = args.Contains("-lowres");
            bool gray = args.Contains("-gray");

            // ** Check that the diretory exists **
            if (!Directory.Exists(directory_path))
                throw new Exception($"ERROR: Directory [{directory_path}] doesn't exist!"); ;

            // ** Print all files inside the diretory **
            if(print_files)
            {
                Console.WriteLine("Printing file names...");
                PrintFileNames(directory_path);
            }

            // ** Rename all files inside the diretory **
            if (rename_files)
            {
                Console.WriteLine("Renaming files...");
                RenameFiles(directory_path);
            }

            // ** BMP --> JPEG **
            if (bmp_to_jpg)
            {
                Console.WriteLine("Converting BMP to JPEG...");
                BmpToJpeg(directory_path);
            }

            var dir_info = new DirectoryInfo(directory_path);
            var parent_dir = dir_info.Parent.FullName;
            var low_rez_dir = Path.Combine(parent_dir, $"{dir_info.Name} (Low Resolution)");
            var grayscale_dir = Path.Combine(parent_dir, $"{dir_info.Name} (Grayscale)");

            // ** Lower images' resolution **
            if (lowres)
            {
                Console.WriteLine("Lowering images' resolution...");

                if (!Directory.Exists(low_rez_dir))
                    Directory.CreateDirectory(low_rez_dir);

                CreateLowResolutionImages(
                    directory_path,
                    low_rez_dir,
                    320, 240);
            }

            // ** Convert images to grayscale8bpp **
            if(gray)
            {
                Console.WriteLine("Converting images to grayscale8bpp...");

                if (!Directory.Exists(grayscale_dir))
                    Directory.CreateDirectory(grayscale_dir);

                ConvertToGrayscale8bpp(
                    Directory.Exists(low_rez_dir) ? low_rez_dir : directory_path,
                    grayscale_dir);
            }

            // ** Create TSV dataset file for each directory (to be consumed later by ML.NET) **
            CreateTsv(directory_path);

            if (Directory.Exists(low_rez_dir))
                CreateTsv(low_rez_dir);

            if (Directory.Exists(grayscale_dir))
                CreateTsv(grayscale_dir);

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static void PrintFileNames(
            string directory_path)
        {
            var files = Directory.EnumerateFiles(directory_path, "*.*", SearchOption.AllDirectories).ToArray();

            foreach (var file in files)
                Console.WriteLine($"File: {file.Replace(directory_path, "")}");
        }

        private static void RenameFiles(
            string directory_path)
        {
            var files = Directory.EnumerateFiles(directory_path, "*.*", SearchOption.AllDirectories).ToArray();

            foreach (var file in files)
            {
                var subdir = Path.GetDirectoryName(file);
                var ext = Path.GetExtension(file);
                var newname = $"{Path.GetFileNameWithoutExtension(Path.GetRandomFileName())}{ext}";
                File.Move(file, Path.Combine(subdir, newname));
            }
        }

        private static void BmpToJpeg(
            string directory_path)
        {
            var files = Directory.EnumerateFiles(directory_path, "*.*", SearchOption.AllDirectories).ToArray();

            foreach (var file in files)
            {
                if (Path.GetExtension(file) == ".bmp")
                {
                    var subdir = Path.GetDirectoryName(file);
                    var filename = Path.GetFileNameWithoutExtension(file);
                    var newname = Path.Combine(subdir, $"{filename}.jpg");

                    if(!File.Exists(newname))
                    {
                        using (var bitmap = new Bitmap(file))
                            SaveAsJpeg(bitmap, newname);
                    }
                }
            }
        }

        private static void CreateLowResolutionImages(
            string source_directory_path,
            string dest_directory_path,
            int width,
            int height)
        {
            var files = Directory.EnumerateFiles(source_directory_path, "*.*", SearchOption.AllDirectories).ToArray();

            foreach (var file in files)
            {
                if (Path.GetExtension(file) == ".jpg")
                {
                    var subdir = Path.GetDirectoryName(file).Replace(source_directory_path, dest_directory_path);
                    if (!Directory.Exists(subdir))
                        Directory.CreateDirectory(subdir);

                    var filename = Path.GetFileName(file);

                    using (var bitmap = new Bitmap(file))
                    using(var resized_bitmap = ResizeImage(bitmap, width, height))
                        SaveAsJpeg(resized_bitmap, Path.Combine(subdir, filename));
                }
            }
        }

        private static Bitmap ResizeImage(
            Image image,
            int width,
            int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);

                    graphics.DrawImage(
                        image,
                        destRect,
                        0, 0,
                        image.Width, image.Height,
                        GraphicsUnit.Pixel,
                        wrapMode);
                }
            }

            return destImage;
        }

        private static void SaveAsJpeg(
            Bitmap bitmap,
            string filename)
        {
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 100L);
            bitmap.Save(filename, GetEncoder(ImageFormat.Jpeg), encoderParams);

            ImageCodecInfo GetEncoder(ImageFormat format)
                => ImageCodecInfo.GetImageEncoders().First(codec => codec.FormatID == format.Guid);
        }

        private static void ConvertToGrayscale8bpp(
            string source_directory_path,
            string dest_directory_path)
        {
            var files = Directory.EnumerateFiles(source_directory_path, "*.*", SearchOption.AllDirectories).ToArray();

            foreach (var file in files)
            {
                if (Path.GetExtension(file) == ".jpg")
                {
                    var subdir = Path.GetDirectoryName(file).Replace(source_directory_path, dest_directory_path);
                    if (!Directory.Exists(subdir))
                        Directory.CreateDirectory(subdir);

                    var filename = Path.GetFileName(file);

                    using (var bitmap = new Bitmap(file))
                    using (var grayscale_bitmap = ConvertToGrayscale8bpp(bitmap))
                        SaveAsJpeg(grayscale_bitmap, Path.Combine(subdir, filename));
                }
            }
        }

        private static Bitmap ConvertToGrayscale8bpp(
            Bitmap bitmap)
        {
            Bitmap grayScaleBitmap = new Bitmap(
                bitmap.Width, bitmap.Height,
                PixelFormat.Format8bppIndexed);

            ColorPalette pal = grayScaleBitmap.Palette;
            for (int i = 0; i <= 255; i++)
                pal.Entries[i] = Color.FromArgb(i, i, i);
            grayScaleBitmap.Palette = pal;

            var data = grayScaleBitmap.LockBits(
                new Rectangle(0, 0, grayScaleBitmap.Width, grayScaleBitmap.Height),
                ImageLockMode.ReadWrite,
                grayScaleBitmap.PixelFormat);

            for (int x = 0; x < grayScaleBitmap.Width; x++)
            {
                for (int y = 0; y < grayScaleBitmap.Height; y++)
                {
                    Color color = bitmap.GetPixel(x, y);
                    var gray = (byte)(color.R * 0.3 + color.G * 0.59 + color.B * 0.11);

                    Marshal.WriteInt16(data.Scan0, y * data.Stride + x, gray);
                }
            }

            grayScaleBitmap.UnlockBits(data);

            return grayScaleBitmap;
        }

        private static void CreateTsv(
            string directory_path)
        {
            var dir_info = new DirectoryInfo(directory_path);
            var parent_dir = dir_info.Parent.FullName;

            var files = Directory.EnumerateFiles(directory_path, "*.*", SearchOption.AllDirectories).ToArray();

            using (var text_writer = File.CreateText(Path.Combine(parent_dir, $"{dir_info.Name}.tsv")))
            {
                text_writer.WriteLine("Label\tImageSource");
                
                foreach (var file in files)
                {
                    var subdir = Path.GetDirectoryName(file);
                    var label = subdir.Replace(directory_path, "").Trim(Path.DirectorySeparatorChar);

                    text_writer.WriteLine($"{label}\t{file}");
                }
            }
        }
    }
}
