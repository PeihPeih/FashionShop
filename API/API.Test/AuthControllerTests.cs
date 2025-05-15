using API.Controllers;
using API.Data;
using API.Dtos;
using API.Helper.Factory;
using API.Helpers;
using API.Models;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace API.Tests.Controllers
{
    public class AuthControllerTests : IDisposable
    {
        private readonly DPContext _context;
        private readonly DbContextOptions<DPContext> _options;
        private readonly Mock<UserManager<AppUser>> _mockUserManager;
        private readonly Mock<IJwtFactory> _mockJwtFactory;
        private readonly Mock<IOptions<JwtIssuerOptions>> _mockJwtOptions;
        private readonly Mock<IMapper> _mockMapper;
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            // Setup in-memory database
            _options = new DbContextOptionsBuilder<DPContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new DPContext(_options);

            // Setup mocks
            var userStoreMock = new Mock<IUserStore<AppUser>>();
            _mockUserManager = new Mock<UserManager<AppUser>>(
                userStoreMock.Object, null, null, null, null, null, null, null, null);

            _mockJwtFactory = new Mock<IJwtFactory>();

            var jwtOptions = new JwtIssuerOptions
            {
                Issuer = "TestIssuer",
                Audience = "TestAudience",
                ValidFor = TimeSpan.FromMinutes(30)
            };
            var mockOptions = new Mock<IOptions<JwtIssuerOptions>>();
            mockOptions.Setup(m => m.Value).Returns(jwtOptions);
            _mockJwtOptions = mockOptions;

            _mockMapper = new Mock<IMapper>();

            _controller = new AuthController(
                _mockUserManager.Object,
                _mockMapper.Object,
                _context,
                _mockJwtFactory.Object,
                _mockJwtOptions.Object);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region RegisterCustomer Tests

        [Fact]
        public async Task TC_AUTH_01_RegisterCustomer_Success()
        {
            // Arrange
            var registrationData = new RegistrationViewModel
            {
                Email = "test@example.com",
                Password = "Test@123",
                FirstName = "Nguyễn",
                LastName = "Văn A",
                Location = "Hà Nội",
                Quyen = null // Will be set to "Customer" in controller
            };

            var jsonData = new JObject();
            jsonData["data"] = JObject.FromObject(registrationData);

            var newAppUser = new AppUser
            {
                Id = "user123",
                UserName = "test@example.com",
                Email = "test@example.com",
                FirstName = "Nguyễn",
                LastName = "Văn A",
                Quyen = "Customer"
            };

            _mockMapper.Setup(m => m.Map<AppUser>(It.IsAny<RegistrationViewModel>()))
                .Returns(newAppUser);

            _mockUserManager.Setup(m => m.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _controller.Post(jsonData);

            // Assert
            var okResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            // Check DB
            var jobSeeker = await _context.JobSeekers.FirstOrDefaultAsync(j => j.Id_Identity == "user123");
            Assert.NotNull(jobSeeker);
            Assert.Equal("Hà Nội", jobSeeker.Location);
        }

        [Fact]
        public async Task TC_AUTH_02_RegisterCustomer_Invalid_Data()
        {
            // Arrange
            var registrationData = new RegistrationViewModel
            {
                Email = "invalidemail", // Invalid email format
                Password = "123", // Too short password
                FirstName = "Nguyễn",
                LastName = "Văn A",
                Location = "Hà Nội"
            };

            var jsonData = new JObject();
            jsonData["data"] = JObject.FromObject(registrationData);

            _controller.ModelState.AddModelError("Email", "Invalid email format");

            // Act
            var result = await _controller.Post(jsonData);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);

            // Check DB
            var jobSeekerCount = await _context.JobSeekers.CountAsync();
            Assert.Equal(0, jobSeekerCount);
        }

        [Fact]
        public async Task TC_AUTH_03_RegisterCustomer_UserCreation_Failed()
        {
            // Arrange
            var registrationData = new RegistrationViewModel
            {
                Email = "test@example.com",
                Password = "Test@123",
                FirstName = "Nguyễn",
                LastName = "Văn A",
                Location = "Hà Nội"
            };

            var jsonData = new JObject();
            jsonData["data"] = JObject.FromObject(registrationData);

            var newAppUser = new AppUser
            {
                Id = "user123",
                UserName = "test@example.com",
                Email = "test@example.com",
                FirstName = "Nguyễn",
                LastName = "Văn A"
            };

            _mockMapper.Setup(m => m.Map<AppUser>(It.IsAny<RegistrationViewModel>()))
                .Returns(newAppUser);

            var identityErrors = new List<IdentityError>
            {
                new IdentityError { Code = "DuplicateEmail", Description = "Email 'test@example.com' is already taken." }
            };

            _mockUserManager.Setup(m => m.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(identityErrors.ToArray()));

            // Act
            var result = await _controller.Post(jsonData);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);

            // Check DB
            var jobSeekerCount = await _context.JobSeekers.CountAsync();
            Assert.Equal(0, jobSeekerCount);
        }

        #endregion

        #region GetDiaChi Tests

        [Fact]
        public async Task TC_AUTH_04_GetDiaChi_Success()
        {
            // Arrange
            var userId = "user123";
            var userAddress = "123 Đường Lê Lợi, Hà Nội";

            await _context.AppUsers.AddAsync(new AppUser
            {
                Id = userId,
                DiaChi = userAddress,
                Email = "test@example.com",
                UserName = "test@example.com"
            });
            await _context.SaveChangesAsync();

            var jsonData = new JObject();
            jsonData["id_user"] = userId;

            // Act
            var result = await _controller.GetDiaChi(jsonData);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.Equal(userAddress, jsonResult.Value);

            // Check DB
            var user = await _context.AppUsers.FindAsync(userId);
            Assert.NotNull(user);
            Assert.Equal(userAddress, user.DiaChi);
        }

        [Fact]
        public async Task TC_AUTH_05_GetDiaChi_NotFound()
        {
            // Arrange
            var nonExistentUserId = "nonexistent";
            var jsonData = new JObject();
            jsonData["id_user"] = nonExistentUserId;

            // Act
            var result = await _controller.GetDiaChi(jsonData);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.Null(jsonResult.Value);

            // Check DB
            var user = await _context.AppUsers.FindAsync(nonExistentUserId);
            Assert.Null(user);
        }

        #endregion

        #region Login Tests

        [Fact]
        public async Task TC_AUTH_06_Login_Success()
        {
            // Arrange
            var credentials = new CredentialsViewModel
            {
                UserName = "test@example.com",
                Password = "Test@123"
            };

            var userId = "user123";
            var claims = new ClaimsIdentity(new[]
            {
                new Claim("id", userId)
            });

            _mockJwtFactory.Setup(f => f.GenerateClaimsIdentity(credentials.UserName, userId))
                .Returns(claims);

            _mockJwtFactory.Setup(f => f.GenerateEncodedToken(credentials.UserName, claims))
                .ReturnsAsync("test-jwt-token");

            _mockUserManager.Setup(m => m.FindByNameAsync(credentials.UserName))
                .ReturnsAsync(new AppUser
                {
                    Id = userId,
                    UserName = credentials.UserName,
                    Email = credentials.UserName,
                    Quyen = "Customer",
                    FirstName = "Nguyễn",
                    LastName = "Văn A"
                });

            _mockUserManager.Setup(m => m.CheckPasswordAsync(It.IsAny<AppUser>(), credentials.Password))
                .ReturnsAsync(true);

            await _context.AppUsers.AddAsync(new AppUser
            {
                Id = userId,
                UserName = credentials.UserName,
                Email = credentials.UserName,
                Quyen = "Customer",
                FirstName = "Nguyễn",
                LastName = "Văn A"
            });
            await _context.SaveChangesAsync();

            // Set static id field using reflection
            var idField = typeof(AuthController).GetField("id",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic);
            idField.SetValue(null, userId);

            // Act
            var result = await _controller.Post(credentials);

            // Assert
            var okObjectResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okObjectResult.StatusCode);

            // Check DB for AuthHistory
            var authHistory = await _context.AuthHistories.FirstOrDefaultAsync(a => a.IdentityId == userId);
            Assert.NotNull(authHistory);
        }

        [Fact]
        public async Task TC_AUTH_07_Login_Invalid_Password()
        {
            // Arrange
            var credentials = new CredentialsViewModel
            {
                UserName = "test@example.com",
                Password = "WrongPassword"
            };

            _mockUserManager.Setup(m => m.FindByNameAsync(credentials.UserName))
                .ReturnsAsync(new AppUser
                {
                    Id = "user123",
                    UserName = credentials.UserName
                });

            _mockUserManager.Setup(m => m.CheckPasswordAsync(It.IsAny<AppUser>(), credentials.Password))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.Post(credentials);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);

            // Check DB
            var authHistoryCount = await _context.AuthHistories.CountAsync();
            Assert.Equal(0, authHistoryCount);
        }

        [Fact]
        public async Task TC_AUTH_08_Login_Invalid_Username()
        {
            // Arrange
            var credentials = new CredentialsViewModel
            {
                UserName = "nonexistent@example.com",
                Password = "Test@123"
            };

            _mockUserManager.Setup(m => m.FindByNameAsync(credentials.UserName))
                .ReturnsAsync((AppUser)null);

            // Act
            var result = await _controller.Post(credentials);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);

            // Check DB
            var authHistoryCount = await _context.AuthHistories.CountAsync();
            Assert.Equal(0, authHistoryCount);
        }

        #endregion

        #region Logout Tests

        [Fact]
        public void TC_AUTH_09_Logout_Success()
        {
            // Arrange - Set the static id (via reflection since it's private static)
            var idField = typeof(AuthController).GetField("id",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic);
            idField.SetValue(null, "user123");

            // Act
            var result = _controller.logout();

            // Assert
            var okResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            // Check static id is null
            var currentId = idField.GetValue(null);
            Assert.Null(currentId);
        }

        #endregion

        #region GetAuthHistory Tests

        [Fact]
        public async Task TC_AUTH_10_GetAuthHistory_Success()
        {
            // Arrange - Set the static id
            var idField = typeof(AuthController).GetField("id",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic);
            var userId = "user123";
            idField.SetValue(null, userId);

            // Add test user to DB
            var testUser = new AppUser
            {
                Id = userId,
                UserName = "test@example.com",
                Email = "test@example.com",
                FirstName = "Nguyễn",
                LastName = "Văn A"
            };
            await _context.AppUsers.AddAsync(testUser);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetAuthHistory();

            // Assert
            var actionResult = Assert.IsType<ActionResult<AppUser>>(result);
            var user = actionResult.Value;

            Assert.NotNull(user);
            Assert.Equal(userId, user.Id);
            Assert.Equal("test@example.com", user.Email);

            // Check DB
            var dbUser = await _context.AppUsers.FindAsync(userId);
            Assert.NotNull(dbUser);
            Assert.Equal(testUser.Email, dbUser.Email);
        }

        [Fact]
        public async Task TC_AUTH_11_GetAuthHistory_NotLoggedIn()
        {
            // Arrange - Set static id to null
            var idField = typeof(AuthController).GetField("id",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic);
            idField.SetValue(null, null);

            // Act
            var result = await _controller.GetAuthHistory();

            // Assert
            var actionResult = Assert.IsType<ActionResult<AppUser>>(result);
            Assert.Null(actionResult.Value);
        }

        #endregion
    }
}
