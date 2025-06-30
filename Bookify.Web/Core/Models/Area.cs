﻿namespace Cover_to_Cover.Web.Core.Models
{
    public class Area : BaseModel
    {
        public int Id { get; set; }

        [MaxLength(100)]
        public string Name { get; set; } = null!;

        public int GovernorateId { get; set; }

        public Governorate? Governorate { get; set; }
    }
}
