﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json.Serialization;

namespace Bit.Core.Vault.Models.Data;

public class CipherAttachment
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public string AttachmentId { get; set; }
    public string AttachmentData { get; set; }

    public class MetaData
    {
        private long _size;

        // We serialize Size as a string since JSON (or Javascript) doesn't support full precision for long numbers
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
        public long Size
        {
            get { return _size; }
            set { _size = value; }
        }

        public string FileName { get; set; }
        public string Key { get; set; }

        public string ContainerName { get; set; } = "attachments";
        public bool Validated { get; set; } = true;

        // This is stored alongside metadata as an identifier. It does not need repeating in serialization
        [JsonIgnore]
        public string AttachmentId { get; set; }

        /// <summary>
        /// Temporary metadata used to store original metadata on migrations from a user-owned attachment to an organization-owned one
        /// </summary>
        public MetaData TempMetadata { get; set; }
    }
}
