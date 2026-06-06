using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using SupMail.Models;

namespace SupMail.Services
{
    public class ZipService
    {
        public async Task<string> CreateZipFromFilesAsync(
            AttachmentContext context,
            List<int> selectedIndices)
        {
            string outputFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, context.DocNum);
            Directory.CreateDirectory(outputFolder);
            string zipPath = Path.Combine(outputFolder, $"{context.DocNum}.zip");

            using (var zipStream = new FileStream(zipPath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                foreach (int idx in selectedIndices)
                {
                    var fileItem = context.Files[idx];
                    string localPath = await context.FileResolver(idx);
                    if (!File.Exists(localPath)) continue;

                    string entryName = Path.GetFileName(localPath);
                    if (string.IsNullOrWhiteSpace(entryName))
                        entryName = fileItem.DisplayName;

                    var entry = archive.CreateEntry(entryName);
                    using var entryStream = entry.Open();
                    using var fileStream = File.OpenRead(localPath);
                    await fileStream.CopyToAsync(entryStream);
                }
            }

            return zipPath;
        }

        public void OpenInExplorer(string zipPath)
        {
            string? folder = Path.GetDirectoryName(zipPath);
            if (!string.IsNullOrEmpty(folder))
                System.Diagnostics.Process.Start("explorer.exe", folder);
        }
    }
}
