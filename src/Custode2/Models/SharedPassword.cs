using System;

namespace Custode2.Models
{
    [Serializable]
    public class SharedPassword
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastModifiedAt { get; set; }

        public SharedPassword()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = string.Empty;
            Value = string.Empty;
            Notes = string.Empty;
            CreatedAt = DateTime.Now;
            LastModifiedAt = DateTime.Now;
        }

        public SharedPassword(string name, string value) : this()
        {
            Name = name;
            Value = value;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? "(Senza nome)" : Name;
        }
    }
}
