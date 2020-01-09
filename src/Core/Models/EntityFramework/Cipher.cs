using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class Cipher : Table.Cipher
    {
        private JsonDocument _dataJson;
        private JsonDocument _attachmentsJson;

        public User User { get; set; }
        public Organization Organization { get; set; }
        public JsonDocument DataJson
        {
            get => _dataJson;
            set
            {
                Data = value.ToString();
                _dataJson = value;
            }
        }
        public JsonDocument AttachmentsJson
        {
            get => _attachmentsJson;
            set
            {
                Attachments = value.ToString();
                _attachmentsJson = value;
            }
        }
    }

    public class CipherMapperProfile : Profile
    {
        public CipherMapperProfile()
        {
            CreateMap<Table.Cipher, Cipher>().ReverseMap();
        }
    }
}
