using back.Models;

namespace back.Services
{
    public interface IPasswordService
    {
        string HashPassword(Users user, string password);
        bool VerifyPassword(Users user, string password);
    }
}
