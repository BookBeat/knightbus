using System;
using System.Collections.Generic;

namespace KnightBus.Core
{
    public static class AttachmentUtility
    {
        public const string AttachmentKey = "_attachments";

        public static string[] GetAttachmentIds(IDictionary<string, string> properties)
        {
            if (properties != null && properties.TryGetValue(AttachmentKey, out var stringAttachments))
            {
                return stringAttachments.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            }
            return new string[0];
        }
    }
}