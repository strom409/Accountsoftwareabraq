namespace AbraqAccount.Models.Common;

public class UserSession
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Role { get; set; }
    public DateTime LoginTime { get; set; } = DateTime.Now;
}
