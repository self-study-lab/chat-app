using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CleanArchitecture.Domain.Model.Player;
using CleanArchitecture.Domain.Model.VerificationCode;

namespace CleanArchitecture.Application.Repository
{
    public interface IPlayerRepository
    {
        Task<List<Player>> GetMembers();
        Task<Player?> GetMemberById(string m);
        Task<Player> LoginMember(string email, string password);
        Task DeleteMember(string m);
        Task UpdateMember(Player m);
        Task AddMember(Player m);
        Task ChangePassword(string id, string hashedPassword, string saltPassword);
        Task<VerificationCode> GetVerificationCodeByUsername(string username);
        Task AddVerificationCode(VerificationCode newCode);
        Task RefreshVerificationCode(VerificationCode newCode);
        Task VerifyAccount(VerificationCode newCode);
    }
}
