using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SupMail.Models;

namespace SupMail.Services
{
    public class OutlookService
    {
        public async Task CreateEmailWithAttachmentsAsync(
            AttachmentContext context,
            List<int> selectedIndices)
        {
            Type? outlookType = Type.GetTypeFromProgID("Outlook.Application");
            if (outlookType == null)
                throw new Exception("Outlook is not installed.");

            dynamic outlookApp = Activator.CreateInstance(outlookType)!;
            dynamic mail = outlookApp.CreateItem(0);
            mail.To = context.Recipient;
            mail.Subject = $"Purchase Order {context.DocNum}";

            int addedAttachments = 0;
            foreach (int idx in selectedIndices)
            {
                var fileItem = context.Files[idx];
                string fullPath = await context.FileResolver(idx);

                if (File.Exists(fullPath))
                {
                    string displayName = fileItem.DisplayName;
                    if (!string.IsNullOrWhiteSpace(displayName))
                        mail.Attachments.Add(fullPath, 1, Type.Missing, displayName);
                    else
                        mail.Attachments.Add(fullPath);
                    addedAttachments++;
                }
            }

            if (addedAttachments == 0)
                throw new Exception("No valid attachment files were found on disk.");

            mail.Display();
        }
    }
}
