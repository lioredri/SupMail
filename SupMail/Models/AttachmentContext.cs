using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SupMail.Models
{
    public class AttachmentContext
    {
        public required string DocNum { get; init; }
        public required string Recipient { get; init; }
        public required List<FileItem> Files { get; init; }
        public required Func<int, Task<string>> FileResolver { get; init; }
    }
}
