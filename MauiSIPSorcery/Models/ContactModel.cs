using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MauiSIPSorcery.Models
{
    public class ContactModel
    {
        public int ContactId { get; set; }
        public int UserId { get; set; }
        public int ContactUserId { get; set; }
        public string Alias { get; set; }
        public DateTime CreatedAt { get; set; }


        public string ContactUsername { get; set; }

        public byte[] ContactUserIcon { get; set; }

        public string ContactUserStatus { get; set; }


        [JsonIgnore]
        public ImageSource ContactUserIcon_Show
        {
            get { return ContactUserIcon == null ? null : ImageSource.FromStream(() => new MemoryStream(ContactUserIcon)); }
        }
    }
}
