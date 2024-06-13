using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AuthMicroservice.Model
{
    public class BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        [Required]
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; } = false;
        public string? Remarks { get; set; }
        public DateTime CreatedBy { get; set; }
        public DateTime? UpdatedBy { get; set; }

        public BaseEntity()
        {
            IsActive = true; 
            IsDeleted=false;
            CreatedDate = DateTime.Now;
        }
    }
}
