using System.Text.Json.Serialization;

namespace Conduit.Features.Users;

public class Edit
{
    // setters record which fields were present in the request body: the RealWorld spec
    // distinguishes an absent field (keep current value) from an explicit null (reject,
    // or clear for the nullable bio/image fields)
    public class UserData
    {
        private string? _username;
        private string? _email;
        private string? _password;
        private string? _bio;
        private string? _image;

        public string? Username
        {
            get => _username;
            set
            {
                _username = value;
                UsernameSet = true;
            }
        }

        public string? Email
        {
            get => _email;
            set
            {
                _email = value;
                EmailSet = true;
            }
        }

        public string? Password
        {
            get => _password;
            set
            {
                _password = value;
                PasswordSet = true;
            }
        }

        public string? Bio
        {
            get => _bio;
            set
            {
                _bio = value;
                BioSet = true;
            }
        }

        public string? Image
        {
            get => _image;
            set
            {
                _image = value;
                ImageSet = true;
            }
        }

        [JsonIgnore]
        public bool UsernameSet { get; private set; }

        [JsonIgnore]
        public bool EmailSet { get; private set; }

        [JsonIgnore]
        public bool PasswordSet { get; private set; }

        [JsonIgnore]
        public bool BioSet { get; private set; }

        [JsonIgnore]
        public bool ImageSet { get; private set; }
    }
}
