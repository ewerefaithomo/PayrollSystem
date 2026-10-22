using System.ComponentModel.DataAnnotations;

public class User
{
    public int Id { get; set; }

    [Required]
    public string FirstName { get; set; }

    [Required]
    public string Surname { get; set; }

    public string Designation { get; set; }

    [Required]
    public string Username { get; set; }

    [Required]
    public string Password { get; set; }
}