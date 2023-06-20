using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.RegularExpressions;
using WebApplication1.Data;
using WebApplication1.Data.Entities;
using WebApplication1.Models.User;
using WebApplication1.Servicies.Email;
using WebApplication1.Servicies.Hash;

namespace WebApplication1.Controllers
{
    public class UserController : Controller
    {
        private readonly DataContext _dataContext;
        private readonly IHashService _hashService;
        private readonly ILogger<UserController> _logger;
        private readonly IEmailService _emailService;
        private readonly IUserServiceEmail _userService;
        public UserController(DataContext dataContext, IHashService hashService, ILogger<UserController> logger, IEmailService emailService, IUserServiceEmail userService)
        {
            _dataContext = dataContext;
            _hashService = hashService;
            _logger = logger;
            _emailService = emailService;
            _userService = userService;
        }

        public JsonResult UpdateEmail(string email)
        {
            //_logger.LogInformation("UpdateEmail works");
            if (HttpContext.User.Identity?.IsAuthenticated != true)
            {
                return Json(new { success = false, message = "Unauthenticated" });
            }

            Guid userId;
            try
            {
                userId = Guid.Parse(HttpContext.User.Claims.First(c => c.Type == ClaimTypes.Sid).Value);
            }
            catch (Exception ex)
            {
                _logger.LogError("UpdateEmail exception {ex}", ex.Message);
                return Json(new { success = false, message = "Unauthorized" });
            }

            var user = _dataContext.Users.Find(userId);
            if (user == null)
            {
                return Json(new { success = false, message = "Access denied" });
            }

            if (user.Email == email)
            {
                return Json(new { success = false, message = "Email is the same" });
            }

            String confirmCode = Guid.NewGuid().ToString()[..6].ToUpperInvariant();
            try
            {
                _emailService.Send(email, $"To confirm email enter code <b>{confirmCode}</b>", "Email changed");
            }
            catch (Exception ex)
            {
                _logger.LogError("UpdateEmail exception {ex}", ex.Message);
                return Json(new { success = false, message = "Invalid email" });
            }

            user.Email = email;
            user.ConfirmCode = confirmCode;

            _dataContext.SaveChanges();
            return Json(new { success = true });
        }
        public JsonResult ConfirmCode(string code)
        {
            if (HttpContext.User.Identity?.IsAuthenticated != true)
            {
                return Json(new { success = false, message = "Unauthenticated" });
            }

            Guid userId;
            try
            {
                userId = Guid.Parse(HttpContext.User.Claims.First(c => c.Type == ClaimTypes.Sid).Value);
            }
            catch (Exception ex)
            {
                _logger.LogError("ConfirmCode exception {ex}", ex.Message);
                return Json(new { success = false, message = "Unauthorized" });
            }

            var user = _dataContext.Users.Find(userId);
            if (user == null)
            {
                return Json(new { success = false, message = "Access denied" });
            }

            if (user.ConfirmCode != code)
            {
                return Json(new { success = false, message = "Invalid code" });
            }

            user.ConfirmCode = null;
            _dataContext.SaveChanges();

            return Json(new { success = true });
        }
        public ViewResult Profile()
        {
            // находим ид пользователя из Claims
            String? userId = HttpContext.User.Claims
                .FirstOrDefault(c => c.Type == ClaimTypes.Sid)?.Value;


            ProfileViewModel model = null!;
            if(userId is not null)
            {
                var user = _dataContext.Users.Find(Guid.Parse(userId));
                if (user != null)
                {
                    model = new()
                    {
                        Name = user.Name,
                        Email= user.Email,
                        Avatar= user.Avatar ?? "no-photo.png",    
                        CreatedDt= user.CreatedDt,
                        Login = user.Login,
                        IsEmailConfirmed = user.ConfirmCode == null
                    };
                }
            }
            return View(model);
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public JsonResult Auth([FromBody]AuthAjaxModel model)
        {
            var user = _dataContext.Users.FirstOrDefault(
                u => u.Login == model.Login 
                && u.PasswordHash ==
                _hashService.GetHash(model.Password));
            if (user != null) 
            {
                HttpContext.Session.SetString("userId", user.Id.ToString());
            }
            return Json(new { Success = (user != null) });
        }
        public RedirectToActionResult Logout()
        {
            HttpContext.Session.Remove("userId"); 

            //return Json(new { Success = true });
            return RedirectToAction("Index","Home");
        }
        public IActionResult SignUp(SignUpFormModel? formModel)
        {
            SignUpViewModel viewModel = new();
            if (Request.Method == "POST" && formModel != null)
            {
                viewModel = ValidateSignUpForm(formModel);
                viewModel.FormModel = formModel;

                HttpContext.Session.SetString("FormData",
                    System.Text.Json.JsonSerializer.Serialize(viewModel)); 
                return RedirectToAction(nameof(SignUp));
            }
            else
            {
                if (HttpContext.Session.Keys.Contains("FormData"))
                {
                    String? data = HttpContext.Session.GetString("FormData");
                    if(data != null)
                    {
                        viewModel = System.Text.Json.
                        JsonSerializer.Deserialize<SignUpViewModel>(data)!;
                    }
                    HttpContext.Session.Remove("FormData");
                }
                else
                {
                    viewModel = new();
                    viewModel.FormModel = null; // нечего проверять
                }
            }
            return View(viewModel);
        }
        public IActionResult EditableBlur(string userId, string newEmail)
        {
            User user = _userService.GetUserById(userId);

            user.Email = newEmail;

            _userService.UpdateUser(user);

            string subject = "Изменение почты";
            string message = $"Ваша новая почта: {newEmail}";
            _emailService.Send(user.Email, message, subject);

            return RedirectToAction("Index", "Home");
        }
        private SignUpViewModel ValidateSignUpForm(SignUpFormModel formModel)
        {
            SignUpViewModel viewModel = new();

            #region Email Validation
            if (string.IsNullOrEmpty(formModel.Email))
            {
                viewModel.EmailMessage = "Логин не может быть пустым";
            }
            else if (formModel.Email.Length < 3)
            {
                viewModel.EmailMessage = "Логин слишком короткий (минимум 3 символа)";
            }
            else if (_dataContext.Users.Any(u => u.Email == formModel.Email))
            {
                viewModel.EmailMessage = "Данный логин уже занят";
            }
            #endregion

            #region Password Validation
            if (string.IsNullOrEmpty(formModel.Password))
            {
                viewModel.PasswordMessage = "Пароль не может быть пустым";
            }
            else if (formModel.Password.Length < 3)
            {
                viewModel.PasswordMessage = "Пароль слишком короткий (минимум 3 символа)";
            }
            else if (!Regex.IsMatch(formModel.Password, @"\d"))
            {
                viewModel.PasswordMessage = "Пароль должен содержать цифры";
            }
            else
            {
                viewModel.PasswordMessage = null;
            }
            #endregion

            #region Email Validation
            if (string.IsNullOrEmpty(formModel.Email))
            {
                viewModel.EmailMessage = "Email не может быть пустым";
            }
            else if (formModel.Email.Length < 7)
            {
                viewModel.EmailMessage = "Некорректный формат email";
            }
            else
            {
                viewModel.EmailMessage = null;
            }
            #endregion

            #region Real Name Validation
            if (string.IsNullOrEmpty(formModel.RealName))
            {
                viewModel.RealNameMessage = "Имя не может быть пустым";
            }
            else
            {
                viewModel.RealNameMessage = null;
            }
            #endregion
            #region Avatar
            string? nameAvatar = null;
            if (formModel.Avatar != null)
            {
                if (formModel.Avatar.Length > 1048576)
                {
                    viewModel.AvatarMessage = "Файл слишком большой (макс 1 МБ)";
                }
                String ext = Path.GetExtension(formModel.Avatar.FileName);

                nameAvatar = Guid.NewGuid().ToString() + ext;

                formModel.Avatar.CopyTo(
                    new FileStream("wwwroot/avatars/" + nameAvatar, FileMode.Create));
            }
            else
            {
                nameAvatar = "no-photo.png";
            }
            #endregion


            if (viewModel.EmailMessage == null && 
                viewModel.PasswordMessage == null &&
                viewModel.AvatarMessage == null)
            {
                _dataContext.Users.Add(new()
                {
                    Id = Guid.NewGuid(),
                    Login = formModel.Login,
                    PasswordHash = _hashService.GetHash(formModel.Password),
                    Email = formModel.Email,
                    CreatedDt = DateTime.Now,
                    Name = formModel.RealName!,
                    Avatar = nameAvatar
                });
                _dataContext.SaveChanges();


                viewModel.SuccessMessage = "Регистрация прошла успешно";
                formModel.Login = string.Empty;
                formModel.Password = string.Empty;
                formModel.Email = string.Empty;
                formModel.RealName = string.Empty;
                viewModel.LoginMessage = null;
                viewModel.PasswordMessage = null;
                viewModel.EmailMessage = null;
                viewModel.RealNameMessage = null;
                viewModel.AvatarMessage = null;
                
            }

            return viewModel;
        }
    }
}
