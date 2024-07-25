﻿using System.ComponentModel.DataAnnotations;

namespace Models.Entities
{
    public class GuildQuote
    {
        [Key]
        public long GuildQuoteId { get; set; }

        public long GuildId { get; set; }

        public string Quote { get; set; } = string.Empty;
    }
}