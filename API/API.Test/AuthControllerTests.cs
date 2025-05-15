using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using API.Controllers;
using API.Data;
using API.Dtos;
using API.Helper.Factory;
using API.Models;
using System.Security.Claims;
using System;

namespace API.Test
{
    public class AuthControllerTests
    {
        private readonly Mock<UserManager<AppUser>> _userManagerMock;
        private readonly Mock<IJwtFactory> _jwtFactoryMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<IOptions<JwtIssuerOptions>> _jwtOptionsMock;
        private readonly DPContext _context;
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            var options = new DbContextOptionsBuilder<DPContext>()
                .UseInMemoryDatabase(databaseName: "TestDb")
                .Options;
            _context = new DPContext(options);

            _userManagerMock = new Mock<UserManager<AppUser>>(
                Mock.Of<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);
            _jwtFactoryMock = new Mock<IJwtFactory>();
            _mapperMock = new Mock<IMapper>();
            _jwtOptionsMock = new Mock<IOptions<JwtIssuerOptions>>();

            _controller = new AuthController(
                _userManagerMock.Object,
                _mapperMock.Object,
                _context,
                _jwtFactoryMock.Object,
                _jwtOptionsMock.Object
            );
        }

        // Test khi ModelState không hợp lệ
        [Fact]
        public async Task UpdateUser_InvalidModel_ReturnsBadRequest()
        {
            // Arrange
            _controller.ModelState.AddModelError("UserName", "Required");
            var credentials = new CredentialsViewModel();

            // Act
            var result = await _controller.UpdateUser(credentials);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        // Test khi username hoặc password không đúng
        [Fact]
        public async Task UpdateUser_InvalidCredentials_ReturnsBadRequest()
        {
            // Arrange
            var credentials = new CredentialsViewModel { UserName = "testuser", Password = "wrongpassword" };
            _controller.ModelState.Clear();

            var identityMock = Task.FromResult<ClaimsIdentity>(null);
            _controller.GetType().GetMethod("GetClaimsIdentity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(_controller, new object[] { credentials.UserName, credentials.Password });

            // Act
            var result = await _controller.UpdateUser(credentials);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        // Test khi username và password đúng, trả về token hợp lệ
        //[Fact]
        //public async Task UpdateUser_ValidCredentials_ReturnsOkObjectResult()
        //{
        //    var registrationData = new JObject
        //    {
        //        ["data"] = JObject.FromObject(new RegistrationViewModel
        //        {
        //            Email = "admin123@gmail.com",
        //            FirstName = "Chu",
        //            LastName = "Hieu",
        //            Password = "Test@123",
        //            Location = "Hanoi"
        //        })
        //    };

        //    _mapperMock.Setup(m => m.Map<AppUser>(It.IsAny<RegistrationViewModel>()))
        //        .Returns((RegistrationViewModel model) => new AppUser
        //        {
        //            UserName = model.Email,
        //            Email = model.Email,
        //            Quyen = model.Quyen,
        //            Id = Guid.NewGuid().ToString()
        //        });

        //    _userManagerMock.Setup(u => u.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
        //        .ReturnsAsync(IdentityResult.Success)
        //        .Callback<AppUser, string>((user, pass) => {
        //            _context.AppUsers.Add(user);
        //            _context.SaveChanges();
        //        });

        //    await _controller.Post(registrationData);

        //    // Arrange
        //    var credentials = new CredentialsViewModel { UserName = "admin123@gmail.com", Password = "Test@123" };
        //    _controller.ModelState.Clear();

        //    var claims = new List<Claim> { new Claim("id", "1") };
        //    var identity = new ClaimsIdentity(claims);

        //    _jwtFactoryMock.Setup(j => j.GenerateEncodedToken(credentials.UserName, identity)).ReturnsAsync("fake_token");

        //    // Act
        //    var result = await _controller.UpdateUser(credentials);

        //    // Assert
        //    var okResult = Assert.IsType<OkObjectResult>(result);
        //    Assert.Contains("auth_token", okResult.Value.ToString());
        //}


        // Test khi người dùng không tồn tại trong database
        [Fact]
        public async Task UpdateUser_UserNotFound_ReturnsBadRequest()
        {
            // Arrange
            var credentials = new CredentialsViewModel { UserName = "nonexistentuser", Password = "Test@123" };
            _controller.ModelState.Clear();

            var claims = new List<Claim> { new Claim("id", "999") }; // ID không tồn tại trong database
            var identity = new ClaimsIdentity(claims);

            _jwtFactoryMock.Setup(j => j.GenerateEncodedToken(credentials.UserName, identity)).ReturnsAsync("fake_token");

            // Act
            var result = await _controller.UpdateUser(credentials);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}
