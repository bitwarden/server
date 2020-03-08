using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class Cipher : Table.Cipher
    {
        private JsonDocument _dataJson;
        private JsonDocument _attachmentsJson;
        private JsonDocument _favoritesJson;
        private JsonDocument _foldersJson;

        public User User { get; set; }
        public Organization Organization { get; set; }
        [IgnoreMap]
        public JsonDocument DataJson
        {
            get => _dataJson;
            set
            {
                Data = value?.ToString();
                _dataJson = value;
            }
        }
        [IgnoreMap]
        public JsonDocument AttachmentsJson
        {
            get => _attachmentsJson;
            set
            {
                Attachments = value?.ToString();
                _attachmentsJson = value;
            }
        }
        [IgnoreMap]
        public JsonDocument FavoritesJson
        {
            get => _favoritesJson;
            set
            {
                Favorites = value?.ToString();
                _favoritesJson = value;
            }
        }
        [IgnoreMap]
        public JsonDocument FoldersJson
        {
            get => _foldersJson;
            set
            {
                Folders = value?.ToString();
                _foldersJson = value;
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
