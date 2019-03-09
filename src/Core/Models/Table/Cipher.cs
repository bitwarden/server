using System;
using Bit.Core.Utilities;
using Bit.Core.Models.Data;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Bit.Core.Models.Table
{
    public class Cipher : ITableObject<Guid>
    {
        private Dictionary<string, CipherAttachment.MetaData> _attachmentData;

        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
        public Guid? OrganizationId { get; set; }
        public Enums.CipherType Type { get; set; }
        public string Data { get; set; }
        public string Favorites { get; set; }
        public string Folders { get; set; }
        public string Attachments { get; set; }
        public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
        public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;
        public DateTime PwnedCheckDate { get; internal set; }
        public bool Pwned { get; internal set; }

        public void SetNewId()
        {
            Id = CoreHelpers.GenerateComb();
        }

        public Dictionary<string, CipherAttachment.MetaData> GetAttachments()
        {
            if(string.IsNullOrWhiteSpace(Attachments))
            {
                return null;
            }

            if(_attachmentData != null)
            {
                return _attachmentData;
            }

            try
            {
                _attachmentData = JsonConvert.DeserializeObject<Dictionary<string, CipherAttachment.MetaData>>(Attachments);
                return _attachmentData;
            }
            catch
            {
                return null;
            }
        }

        public void SetAttachments(Dictionary<string, CipherAttachment.MetaData> data)
        {
            if(data == null || data.Count == 0)
            {
                _attachmentData = null;
                Attachments = null;
                return;
            }

            _attachmentData = data;
            Attachments = JsonConvert.SerializeObject(_attachmentData);
        }

        public void AddAttachment(string id, CipherAttachment.MetaData data)
        {
            var attachments = GetAttachments();
            if(attachments == null)
            {
                attachments = new Dictionary<string, CipherAttachment.MetaData>();
            }

            attachments.Add(id, data);
            SetAttachments(attachments);
        }

        public void DeleteAttachment(string id)
        {
            var attachments = GetAttachments();
            if(!attachments?.ContainsKey(id) ?? true)
            {
                return;
            }

            attachments.Remove(id);
            SetAttachments(attachments);
        }

        public bool ContainsAttachment(string id)
        {
            var attachments = GetAttachments();
            return attachments?.ContainsKey(id) ?? false;
        }
    }
}
