using System.ComponentModel.DataAnnotations;

namespace Models.Entities
{
    public class Raffle
    {
        [Key]
        public int Id { get; set; }

        public string? Description { get; set; }

        public bool IsActive { get; set; }
    }
}
