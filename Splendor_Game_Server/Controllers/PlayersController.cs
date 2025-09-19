using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using CleanArchitecture.Infrastructure.Repository;
using CleanArchitecture.Application.IService;
using CleanArchitecture.Domain.Model.Player;
using CleanArchitecture.Infrastructure.Security;
using Splendor_Game_Server.DTO.Player;
using BusinessObject.DTO;
using Microsoft.AspNetCore.Authorization;
using System.Net.Mail;
using EASendMail;
using GraphQLParser;
using System.IdentityModel.Tokens.Jwt;
using CleanArchitecture.Domain.DTO.Player;
using CleanArchitecture.Domain.Model.VerificationCode;
using System.Numerics;

namespace Splendor_Game_Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlayersController : ControllerBase
    {
        private readonly IPlayerService playerService;
        private readonly SecurityUtility securityUtility;

        public PlayersController(IPlayerService playersService, SecurityUtility securityUtility)
        {
            this.playerService = playersService;
            this.securityUtility = securityUtility;
        }
          

        [HttpGet]
       // [Authorize]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                List<Player> player = await playerService.GetMembers();
                var Count = player.Count();
                return Ok(new { StatusCode = 200, Message = "Load successful", data = player, Count });
            }
            catch (Exception ex)
            {
                return StatusCode(400, new { StatusCode = 400, Message = ex.Message });
            }

        }


        [HttpGet("{id:length(24)}")]
        public async Task<ActionResult> Get(string id)
        {
            try
            {
                var player = await playerService.GetMemberById(id);

                if (player is null)
                {
                    return NotFound();
                }
                return StatusCode(200, new { StatusCode = 200, Message = "Load successful", data = player });
            }
            catch (Exception ex)
            {
                return StatusCode(400, new { StatusCode = 400, Message = ex.Message });
            }
        }
        [HttpPost("Login")]
        public async Task<IActionResult> GetLogin(LoginPlayer acc)
        {
            try
            {
                Player customer = await playerService.LoginMember(acc.Username, acc.Password);
                return Ok(new { StatusCode = 200, Message = "Login succedfully", data = securityUtility.GenerateToken(customer) });
            }
            catch (Exception ex)
            {
                return StatusCode(400, new { StatusCode = 400, Message = ex.Message });
            }
        }
        [HttpPost("Verification_Player")]
        public async Task<IActionResult> VerificationPlayer(VerifyPlayer newVerify)
        {
            try
            {
                VerificationCode listPlayer = await playerService.GetVerificationCodeByUsername(newVerify.Username);
                if(listPlayer is null)
                { return StatusCode(400, new { StatusCode = 400, Message = "Username is not exit" }); }
                await playerService.VerifyAccount(listPlayer, newVerify.Code);
                
                return Ok(new { StatusCode = 200, Message = "Verify Player succedfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(400, new { StatusCode = 400, Message = ex.Message });
            }
        }
        [HttpPost("Refresh_Verification_Code")]
        public async Task<IActionResult> RefreshVerificationCode(RefreshVerificationCodePlayer newVerify)
        {
            try
            {
                string Id = ObjectId.GenerateNewId().ToString();
                string verificationCode = securityUtility.GenerateVerificationCode();
                var newCode = new VerificationCode
                {
                    Id = Id,
                    Code = verificationCode,
                    CreatedAt = DateTime.UtcNow,
                    Email = newVerify.Username
                };
                await playerService.RefreshVerificationCode(newCode);
                return Ok(new { StatusCode = 200, Message = "Load succedfully", data = verificationCode });
            }
            catch (Exception ex)
            {
                return StatusCode(400, new { StatusCode = 400, Message = ex.Message });
            }
        }
        [HttpPost("Register")]
        public async Task<IActionResult> Register(PostPlayer player)
        {
            try
            {
                if (player.ConfirmPassword != player.Password)
                {
                    return StatusCode(400, new { StatusCode = 400, Message = "Confirm Password not correct password" });
                }
                string Id = ObjectId.GenerateNewId().ToString();
                string codeId = ObjectId.GenerateNewId().ToString();
                var saltPassword = securityUtility.GenerateSalt();
                var hashPassword = securityUtility.GenerateHashedPassword(player.Password, saltPassword);
                string verificationCode = securityUtility.GenerateVerificationCode();
                string subject = "Verification Code";
                string email = player.Username;
                string body = @"<!DOCTYPE html>
                              <html lang=""en"">
                              <head>
                              <meta charset=""utf-8"" />
                              <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
                              </head>
                              <body style="" padding: 10px 0; background: #EBF0F5;  width: 100%; height: 100%;"">
                              <div style="" background: white;   padding-left: 40px;padding-right: 40px; padding-top: 50;padding-bottom: 50px;border-radius: 4px;
                              box-shadow: 0 2px 3px #C8D0D8;display: inline-block;margin: 0 auto;"" class=""card"">
                               <img style="""" src=""https://firebasestorage.googleapis.com/v0/b/carmanaager-upload-file.appspot.com/o/images%2Flogo-color%20(1).pngeddab547-393c-4a52-8228-389b00aeacad?alt=media&token=a9fe5d57-b871-46a0-a9da-508902f09832&fbclid=IwAR0naf-IAgqC0ireg_vTPIvu9q0dK_n0gqKdNHhWFhvOyvhjWph-boPTWYk"" />
                              <h1 style="" color: #008CBA;font-size: 24px;"">verify your email to log on to</h1>
                              <h1 style="" color: #008CBA;font-size: 24px;"">Splendor Game</h1>
                              <p>Hello <strong> {email} </strong> </p>
                              <p>We received a login attempt from you with the following code : </p>
                              <button style=""border: 2px;background-color: #9ca3af; ; padding-top: 5px;color: #404F5E;font-family: "" Nunito Sans"", ""Helvetica Neue"" , sans-serif;font-size: 20px;margin: 0;"">"
                              + verificationCode +
                              " </button></body></html>";
                var newPlayer = new Player
                {
                    Id = Id,
                    Name = player.Name,
                    Username = player.Username,
                    HashedPassword = hashPassword,
                    SaltPassword = saltPassword,
                    IsActive = true,
                    IsVerified = false
                };
                await playerService.AddMember(newPlayer);
                var newCode = new VerificationCode
                {
                    Id = codeId,
                    Code = verificationCode,
                    CreatedAt = DateTime.UtcNow,
                    Email = player.Username
                };
                await playerService.AddVerificationCode(newCode);
                SmtpMail oMail = new SmtpMail("TryIt");
                oMail.From = "system.milk.delivery@gmail.com";
                oMail.To = player.Username;
                oMail.Subject = subject;
                oMail.HtmlBody = body;
                SmtpServer oServer = new SmtpServer("smtp.gmail.com");
                oServer.User = "system.milk.delivery@gmail.com";
                oServer.Password = "ukbhmjdaaacdyyxh";

                // Set 465 port
                oServer.Port = 465;

                // detect SSL/TLS automatically
                oServer.ConnectType = SmtpConnectType.ConnectSSLAuto; ;
                EASendMail.SmtpClient oSmtp = new EASendMail.SmtpClient();
                oSmtp.SendMail(oServer, oMail);


                return Ok(new { StatusCode = 200, Message = "Register successful" });
            }
            catch (Exception ex)
            {
                return StatusCode(409, new { StatusCode = 409, Message = ex.Message });
            }
        }
        [HttpPost("Login_Google")]
        public async Task<ActionResult> GetLoginGoogle(string token)
        {
            try
            {
                string Id = ObjectId.GenerateNewId().ToString();
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadJwtToken(token);
                string email = jsonToken.Claims.First(claim => claim.Type == "email").Value;
                string avatar = jsonToken.Claims.First(claim => claim.Type == "picture").Value;
                string name = jsonToken.Claims.First(claim => claim.Type == "name").Value;
                var players = await playerService.GetMembers();
                var isExists = players.SingleOrDefault(x => x.Username == email);
                if (isExists == null)
                {
                    var newPlayer = new Player
                    {
                       Username = email,
                       Id = Id,
                       IsActive = true,
                       IsVerified = true,
                       Name = name,
                    };
                    await playerService.AddMember(newPlayer);
                    var member = players.SingleOrDefault(x => x.Username == newPlayer.Username);
                    return Ok(new { StatusCode = 201, Message = "Login SuccessFully", data = securityUtility.GenerateToken(newPlayer) });
                }
                else
                {
                    return Ok(new { StatusCode = 200, Message = "Login SuccessFully", data = securityUtility.GenerateToken(isExists) });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(409, new { StatusCode = 409, Message = ex.Message });
            }
        }
        [HttpPost]
        public async Task<IActionResult> Post(PostPlayer newplayer)
        {
            try
            {
                string Id = ObjectId.GenerateNewId().ToString();
                var saltPassword = securityUtility.GenerateSalt();
                var hashPassword = securityUtility.GenerateHashedPassword(newplayer.Password, saltPassword);
                Player player = new Player
                {


                    Id = Id,
                    IsActive = true,
                    Name = newplayer.Name,
                    Username = newplayer.Username,
                    HashedPassword = hashPassword,
                    SaltPassword = saltPassword,
                    IsVerified = true
                };
                await playerService.AddMember(player);

                return Ok(new { StatusCode = 200, Message = "Create successful", data = player });
            }
            catch (Exception ex)
            {
                return StatusCode(400, new { StatusCode = 400, Message = ex.Message });
            }
        }

        [HttpPut("{id:length(24)}")]
        public async Task<IActionResult> Update(string id, Player updatedplayer)
        {
            try
            {
                var player = await playerService.GetMemberById(id);

                if (player is null)
                {
                    return NotFound();
                }

                await playerService.UpdateMember(updatedplayer);

                return Ok(new { StatusCode = 200, Message = "Update successful" });
            }
            catch (Exception ex)
            {
                return StatusCode(400, new { StatusCode = 400, Message = ex.Message });
            }
        }

        [HttpDelete("{id:length(24)}")]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                var player = await playerService.GetMemberById(id);

                if (player is null)
                {
                    return NotFound();
                }

                await playerService.DeleteMember(id);

                return Ok(new { StatusCode = 200, Message = "Update successful" });
            }
            catch (Exception ex)
            {
                return StatusCode(400, new { StatusCode = 400, Message = ex.Message });
            }
        }
        [HttpPut("ChangePassword")]
        [Authorize]
        public async Task<IActionResult> ChangPassword(ChangePasswordPlayer player)
        {
            try
            {
                string palyerId = User.FindFirst("Id")?.Value;
                Player oldPlayer = await playerService.GetMemberById(palyerId);
                var saltPassword = securityUtility.GenerateSalt();
                var hashPassword = securityUtility.GenerateHashedPassword(player.OldPassword, saltPassword);
                if (oldPlayer.HashedPassword == null)
                {
                    await playerService.ChangePassword(palyerId, hashPassword, saltPassword);
                    return Ok(new { StatusCode = 200, Message = "ChangePassword successful" });
                }
                if (securityUtility.GenerateHashedPassword(player.OldPassword, oldPlayer.SaltPassword) != oldPlayer.HashedPassword)
                {
                    return Ok(new { StatusCode = 400, Message = "Old Password not correct" });
                }
                else
                {
                    await playerService.ChangePassword(palyerId, hashPassword , saltPassword);
                    return Ok(new { StatusCode = 200, Message = "ChangePassword successful" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(400, new { StatusCode = 400, Message = ex.Message });
            }
        }
    }
}
