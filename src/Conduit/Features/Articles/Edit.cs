using System.Text.Json.Serialization;

namespace Conduit.Features.Articles;

public class Edit
{
    public class ArticleData
    {
        private string[]? _tagList;

        public string? Title { get; set; }

        public string? Description { get; set; }

        public string? Body { get; set; }

        public string[]? TagList
        {
            get => _tagList;
            set
            {
                _tagList = value;
                TagListSet = true;
            }
        }

        [JsonIgnore]
        public bool TagListSet { get; private set; }
    }
}
