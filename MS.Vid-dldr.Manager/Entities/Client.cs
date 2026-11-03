using System.ComponentModel.DataAnnotations;

namespace MS.Vid_dldr.Manager.Entities; 

public class Client 
{
    [Key]
    [Required]
    public int Id { get; set; }
    [Required]
    public required string Name { get; set; }
    [Required]
    public required string Description { get; set; }
    public DateTime? Disabled { get; set; }
}